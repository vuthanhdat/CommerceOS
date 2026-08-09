namespace CommerceOS.Platform.Application.Readiness;

public interface IPlatformReadiness
{
    ReadinessSnapshot GetSnapshot();
}

public sealed record ReadinessSnapshot(string Status, string Service);

