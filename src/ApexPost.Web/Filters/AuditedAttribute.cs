namespace ApexPost.Web.Filters;

// Opts an MVC action (or every action of a controller) into the audit log.
// AuditActionFilter is registered globally but records only endpoints that
// carry this marker, so page views and lookups stay out of the audit store.
[AttributeUsage(
	AttributeTargets.Class | AttributeTargets.Method,
	AllowMultiple = false,
	Inherited = true)]
public sealed class AuditedAttribute : Attribute;
