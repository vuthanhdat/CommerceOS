from __future__ import annotations

import re
from dataclasses import dataclass
from typing import Any


class YamlSubsetError(ValueError):
    pass


_BARE_RE = re.compile(r"^[A-Za-z0-9_.:/+-]+$")


def parse_scalar(raw: str) -> Any:
    text = raw.strip()
    if text == "":
        return None
    if text.startswith("[") and text.endswith("]"):
        return parse_inline_sequence(text)
    if text.startswith('"'):
        import json

        try:
            return json.loads(text)
        except Exception as exc:  # pragma: no cover - defensive
            raise YamlSubsetError(f"invalid quoted scalar: {text}") from exc
    if text.startswith("'") and text.endswith("'"):
        return text[1:-1].replace("''", "'")
    lowered = text.lower()
    if lowered == "true":
        return True
    if lowered == "false":
        return False
    if lowered in {"null", "~"}:
        return None
    if re.fullmatch(r"-?\d+", text):
        return int(text)
    return text


def parse_inline_sequence(raw: str) -> list[Any]:
    text = raw.strip()
    if not (text.startswith("[") and text.endswith("]")):
        raise YamlSubsetError(f"not an inline sequence: {raw}")
    inner = text[1:-1].strip()
    if not inner:
        return []

    items: list[str] = []
    current: list[str] = []
    depth = 0
    quote: str | None = None
    escape = False

    for ch in inner:
        if quote is not None:
            current.append(ch)
            if escape:
                escape = False
            elif ch == "\\" and quote == '"':
                escape = True
            elif ch == quote:
                quote = None
            continue

        if ch in {'"', "'"}:
            quote = ch
            current.append(ch)
            continue
        if ch == "[":
            depth += 1
            current.append(ch)
            continue
        if ch == "]":
            depth -= 1
            if depth < 0:
                raise YamlSubsetError(f"unbalanced inline sequence: {raw}")
            current.append(ch)
            continue
        if ch == "," and depth == 0:
            items.append("".join(current).strip())
            current = []
            continue
        current.append(ch)

    if quote is not None or depth != 0:
        raise YamlSubsetError(f"unterminated inline sequence: {raw}")
    items.append("".join(current).strip())
    return [parse_scalar(item) for item in items]


@dataclass(frozen=True)
class _Line:
    indent: int
    text: str
    number: int


def _prepare(text: str) -> list[_Line]:
    prepared: list[_Line] = []
    for number, raw in enumerate(text.splitlines(), start=1):
        if not raw.strip() or raw.lstrip().startswith("#"):
            continue
        if "\t" in raw[: len(raw) - len(raw.lstrip())]:
            raise YamlSubsetError(f"tabs are not supported at line {number}")
        indent = len(raw) - len(raw.lstrip(" "))
        prepared.append(_Line(indent, raw.strip(), number))
    return prepared


def parse_document(text: str) -> dict[str, Any]:
    lines = _prepare(text)
    if not lines:
        return {}
    value, next_index = _parse_block(lines, 0, lines[0].indent)
    if next_index != len(lines):
        line = lines[next_index]
        raise YamlSubsetError(f"unexpected content at line {line.number}: {line.text}")
    if not isinstance(value, dict):
        raise YamlSubsetError("document root must be a mapping")
    return value


def _parse_block(lines: list[_Line], index: int, indent: int) -> tuple[Any, int]:
    if index >= len(lines):
        return {}, index
    if lines[index].indent != indent:
        raise YamlSubsetError(
            f"unexpected indentation at line {lines[index].number}: {lines[index].text}"
        )
    if lines[index].text.startswith("- ") or lines[index].text == "-":
        return _parse_sequence(lines, index, indent)
    return _parse_mapping(lines, index, indent)


def _parse_mapping(lines: list[_Line], index: int, indent: int) -> tuple[dict[str, Any], int]:
    result: dict[str, Any] = {}
    while index < len(lines):
        line = lines[index]
        if line.indent < indent:
            break
        if line.indent > indent:
            raise YamlSubsetError(f"unexpected indentation at line {line.number}: {line.text}")
        if line.text.startswith("-"):
            break
        if ":" not in line.text:
            raise YamlSubsetError(f"expected mapping entry at line {line.number}: {line.text}")
        key, raw_value = line.text.split(":", 1)
        key = key.strip()
        raw_value = raw_value.strip()
        if not key:
            raise YamlSubsetError(f"empty mapping key at line {line.number}")
        if key in result:
            raise YamlSubsetError(f"duplicate mapping key '{key}' at line {line.number}")

        if raw_value:
            result[key] = parse_scalar(raw_value)
            index += 1
            continue

        index += 1
        if index >= len(lines) or lines[index].indent <= indent:
            result[key] = None
            continue
        child_indent = lines[index].indent
        child, index = _parse_block(lines, index, child_indent)
        result[key] = child
    return result, index


def _parse_sequence(lines: list[_Line], index: int, indent: int) -> tuple[list[Any], int]:
    result: list[Any] = []
    while index < len(lines):
        line = lines[index]
        if line.indent < indent:
            break
        if line.indent != indent or not (line.text.startswith("- ") or line.text == "-"):
            break
        raw_value = line.text[1:].strip()
        if raw_value:
            result.append(parse_scalar(raw_value))
            index += 1
            continue
        index += 1
        if index >= len(lines) or lines[index].indent <= indent:
            result.append(None)
            continue
        child_indent = lines[index].indent
        child, index = _parse_block(lines, index, child_indent)
        result.append(child)
    return result, index


def quote_scalar(value: Any) -> str:
    if isinstance(value, bool):
        return "true" if value else "false"
    if value is None:
        return "null"
    if isinstance(value, int):
        return str(value)
    text = str(value)
    if _BARE_RE.fullmatch(text) and text.lower() not in {"true", "false", "null"}:
        return text
    import json

    return json.dumps(text, ensure_ascii=False)


def render_inline_sequence(values: list[Any] | tuple[Any, ...]) -> str:
    rendered: list[str] = []
    for value in values:
        if isinstance(value, (list, tuple)):
            rendered.append(render_inline_sequence(list(value)))
        else:
            rendered.append(quote_scalar(value))
    return "[" + ", ".join(rendered) + "]"
