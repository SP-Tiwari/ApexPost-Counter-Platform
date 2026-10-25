using System.Reflection;
using System.Reflection.Emit;
using ApexPost.Web.Filters;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;

namespace ApexPost.Web.IntegrationTests;

public sealed class ValidationActionFilterTests
{
	[Fact]
	public async Task InvalidApiModelState_ReturnsValidationProblemWithExistingMessages()
	{
		var context = CreateContext(typeof(ApiValidationProbeController));
		context.ModelState.AddModelError(
			"Name", "The name field is required.");
		var nextCalled = false;
		var filter = new ValidationActionFilter();

		await filter.OnActionExecutionAsync(context, () =>
		{
			nextCalled = true;
			return Task.FromResult(CreateExecutedContext(context));
		});

		Assert.False(nextCalled);
		var badRequest = Assert.IsType<BadRequestObjectResult>(context.Result);
		var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
		Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
		Assert.Equal(
			"The name field is required.",
			Assert.Single(problem.Errors["Name"]));
	}

	[Fact]
	public async Task InvalidModelState_WithAssemblyLevelApiController_ReturnsValidationProblem()
	{
		var controllerType = CreateAssemblyAttributedApiController();
		var context = CreateContext(
			controllerType,
			typeof(ApiValidationProbeController).GetMethod("Action")!);
		context.ModelState.AddModelError("Name", "Assembly-level API error.");
		var nextCalled = false;

		await new ValidationActionFilter().OnActionExecutionAsync(context, () =>
		{
			nextCalled = true;
			return Task.FromResult(CreateExecutedContext(context));
		});

		Assert.False(nextCalled);
		var badRequest = Assert.IsType<BadRequestObjectResult>(context.Result);
		var problem = Assert.IsType<ValidationProblemDetails>(badRequest.Value);
		Assert.Equal("Assembly-level API error.",
			Assert.Single(problem.Errors["Name"]));
	}

	[Fact]
	public async Task ValidApiModelState_ContinuesToAction()
	{
		var context = CreateContext(typeof(ApiValidationProbeController));
		var nextCalled = false;

		await new ValidationActionFilter().OnActionExecutionAsync(context, () =>
		{
			nextCalled = true;
			return Task.FromResult(CreateExecutedContext(context));
		});

		Assert.True(nextCalled);
		Assert.Null(context.Result);
	}

	[Fact]
	public async Task InvalidMvcModelState_ContinuesSoControllerCanRenderItsExistingView()
	{
		var context = CreateContext(typeof(MvcValidationProbeController));
		context.ModelState.AddModelError("Name", "Existing MVC message.");
		var nextCalled = false;

		await new ValidationActionFilter().OnActionExecutionAsync(context, () =>
		{
			nextCalled = true;
			return Task.FromResult(CreateExecutedContext(context));
		});

		Assert.True(nextCalled);
		Assert.Null(context.Result);
		Assert.Equal(
			"Existing MVC message.",
			Assert.Single(context.ModelState["Name"]!.Errors).ErrorMessage);
	}

	private static ActionExecutingContext CreateContext(Type controllerType)
		=> CreateContext(
			controllerType,
			controllerType.GetMethod(
				"Action", BindingFlags.Public | BindingFlags.Instance)!);

	private static ActionExecutingContext CreateContext(
		Type controllerType,
		MethodInfo methodInfo)
	{
		var filters = new List<IFilterMetadata>();
		var descriptor = new ControllerActionDescriptor
		{
			ControllerName = "ValidationProbe",
			ActionName = "Action",
			ControllerTypeInfo = controllerType.GetTypeInfo(),
			MethodInfo = methodInfo,
			RouteValues = new Dictionary<string, string?>
			{
				["controller"] = "ValidationProbe",
				["action"] = "Action"
			}
		};
		var actionContext = new ActionContext(
			new DefaultHttpContext(),
			new RouteData(),
			descriptor);
		return new ActionExecutingContext(
			actionContext,
			filters,
			new Dictionary<string, object?>(),
			new object());
	}

	private static Type CreateAssemblyAttributedApiController()
	{
		var assembly = AssemblyBuilder.DefineDynamicAssembly(
			new AssemblyName($"ApiControllerTestAssembly{Guid.NewGuid():N}"),
			AssemblyBuilderAccess.Run);
		var apiControllerConstructor = typeof(ApiControllerAttribute)
			.GetConstructor(Type.EmptyTypes)!;
		assembly.SetCustomAttribute(
			new CustomAttributeBuilder(apiControllerConstructor, []));

		var typeBuilder = assembly
			.DefineDynamicModule("Main")
			.DefineType("AssemblyAttributedController", TypeAttributes.Public);
		var actionBuilder = typeBuilder.DefineMethod(
			"Action",
			MethodAttributes.Public,
			typeof(void),
			Type.EmptyTypes);
		actionBuilder.GetILGenerator().Emit(OpCodes.Ret);
		return typeBuilder.CreateType()!;
	}

	private static ActionExecutedContext CreateExecutedContext(
		ActionExecutingContext context) =>
		new(context, context.Filters, context.Controller);

	[ApiController]
	private sealed class ApiValidationProbeController
	{
		public void Action() { }
	}

	private sealed class MvcValidationProbeController
	{
		public void Action() { }
	}
}
