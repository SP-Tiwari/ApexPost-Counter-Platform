using Microsoft.AspNetCore.Authorization;

namespace ApexPost.Web.Authorization;

public static class ApexPostPolicies
{
	// Role-based: the signed-in staff member is a Branch Manager. The cookie's
	// role is revalidated against persisted staff on every request.
	public const string BranchManagerOnly = "BranchManagerOnly";

	// Resource-based: used with IAuthorizationService.AuthorizeAsync(user,
	// branchCode, SameBranchOnly) once the resource's branch is known.
	public const string SameBranchOnly = "SameBranchOnly";

	public static void AddApexPostPolicies(this AuthorizationOptions options)
	{
		options.AddPolicy(BranchManagerOnly, policy => policy
			.RequireAuthenticatedUser()
			.RequireRole(nameof(StaffRole.BranchManager)));
		options.AddPolicy(SameBranchOnly, policy => policy
			.RequireAuthenticatedUser()
			.AddRequirements(new SameBranchRequirement()));
	}
}

public sealed class SameBranchRequirement : IAuthorizationRequirement
{
}

// Succeeds when the resource's branch is the active staff member's persisted
// home branch. It deliberately does not trust the BranchCode claim: staff
// records are authoritative (ADR 0002), so a reassigned member is checked
// against their current branch.
public sealed class SameBranchAuthorizationHandler(
	StaffAuthenticationService staffAuthentication)
	: AuthorizationHandler<SameBranchRequirement, BranchCode>
{
	protected override async Task HandleRequirementAsync(
		AuthorizationHandlerContext context,
		SameBranchRequirement requirement,
		BranchCode resource)
	{
		var staffId = context.User.FindFirst("StaffId")?.Value;
		if (string.IsNullOrWhiteSpace(staffId))
			return;

		var staff = await staffAuthentication.FindActiveStaffAsync(staffId)
			.ConfigureAwait(false);
		if (staff is not null && staff.HomeBranchCode == resource)
			context.Succeed(requirement);
	}
}
