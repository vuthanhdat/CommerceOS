# Open Food Facts policy review — 2026-08-14

CommerceOS reviewed the official API documentation for the initial manual URL
adapter. It permits product-data reads, but requires a custom User-Agent and
asks API users to submit its usage form. Published read product limits are 15
requests per minute per IP and search limits are 10 per minute per IP.

The repository therefore does not seed an enabled source or make live calls.
`open-food-facts` may be set to `Enabled`/`Current` only after the operator
records the submitted usage form, contact User-Agent, adapter version and the
same or stricter rate limit in the platform source policy. The adapter accepts
only `https://world.openfoodfacts.org/api/v3.6/product/{barcode}.json`, with no
query string, and keeps external observations as PDI work rather than Catalog
products.
