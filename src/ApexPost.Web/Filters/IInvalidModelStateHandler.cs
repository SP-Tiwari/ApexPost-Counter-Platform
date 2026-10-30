using Microsoft.AspNetCore.Mvc.Filters;

namespace ApexPost.Web.Filters;

// Implemented by MVC controllers whose forms ValidationActionFilter redisplays
// when model binding or validation fails. The controller rebuilds its own view
// model (select lists, branch stock) and clears values that must never be
// echoed back, such as passwords and identity document numbers. Returning null
// leaves the action to run as usual.
public interface IInvalidModelStateHandler
{
	Task<IActionResult?> HandleInvalidModelStateAsync(
		ActionExecutingContext context);
}
