namespace CommerceOS.ProductDataIngestion.Domain;

public readonly record struct DataSourceId(string Value);
public readonly record struct PdiTenantId(string Value);
public enum SourceStatus { Enabled, Paused, Disabled }
public enum PolicyReviewStatus { Current, Stale, Rejected }
public sealed record DataSource(DataSourceId Id, string Name, SourceStatus Status, PolicyReviewStatus PolicyReview, string PolicyVersion, int MaxRequestsPerMinute, long Revision)
{ public bool PlatformEligible => Status is SourceStatus.Enabled && PolicyReview is PolicyReviewStatus.Current; }
public sealed record TenantSourceEnrollment(PdiTenantId TenantId, DataSourceId SourceId, bool Enabled, long Revision);
