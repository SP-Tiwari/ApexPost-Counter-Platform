using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace ApexPost.Web.Filters;

public sealed class ValidationActionFilter : IAsyncActionFilter
{
	public async Task OnActionExecutionAsync(
		ActionExecutingContext context,
		ActionExecutionDelegate next)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(next);

		if (!context.ModelState.IsValid &&
			context.ActionDescriptor is ControllerActionDescriptor descriptor &&
			IsApiController(descriptor.ControllerTypeInfo))
		{
			var problem = new ValidationProblemDetails(context.ModelState)
			{
				Status = StatusCodes.Status400BadRequest
			};
			context.Result = new BadRequestObjectResult(problem);
			return;
		}

		await next().ConfigureAwait(false);
	}

	private static bool IsApiController(TypeInfo controllerType) =>
		controllerType.GetCustomAttribute<ApiControllerAttribute>(
			inherit: true) is not null ||
		controllerType.Assembly.GetCustomAttribute<ApiControllerAttribute>() is not null;
}
