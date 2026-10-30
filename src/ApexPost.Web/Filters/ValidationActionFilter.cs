using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Reflection;

namespace ApexPost.Web.Filters;

// Invalid model state never reaches the action body:
// - [ApiController] endpoints get a ValidationProblemDetails 400 (the built-in
//   ModelStateInvalidFilter is suppressed in Program so this is the one place).
// - MVC form actions are redisplayed through the controller's
//   IInvalidModelStateHandler, which knows how to rebuild its view.
public sealed class ValidationActionFilter : IAsyncActionFilter
{
	public async Task OnActionExecutionAsync(
		ActionExecutingContext context,
		ActionExecutionDelegate next)
	{
		ArgumentNullException.ThrowIfNull(context);
		ArgumentNullException.ThrowIfNull(next);

		if (!context.ModelState.IsValid)
		{
			if (context.ActionDescriptor is ControllerActionDescriptor descriptor &&
				IsApiController(descriptor.ControllerTypeInfo))
			{
				var problem = new ValidationProblemDetails(context.ModelState)
				{
					Status = StatusCodes.Status400BadRequest
				};
				context.Result = new BadRequestObjectResult(problem);
				return;
			}

			if (context.Controller is IInvalidModelStateHandler handler &&
				await handler.HandleInvalidModelStateAsync(context)
					.ConfigureAwait(false) is { } redisplay)
			{
				context.Result = redisplay;
				return;
			}
		}

		await next().ConfigureAwait(false);
	}

	private static bool IsApiController(TypeInfo controllerType) =>
		controllerType.GetCustomAttribute<ApiControllerAttribute>(
			inherit: true) is not null ||
		controllerType.Assembly.GetCustomAttribute<ApiControllerAttribute>() is not null;
}
