using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.DeactivateUser;
using ExamShield.Application.Commands.UpdateUserRole;
using ExamShield.Application.Queries.GetUsers;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users").RequireAuthorization("Administrator");

        group.MapGet("/", async (IMediator mediator, CancellationToken ct, int page = 1, int pageSize = 50) =>
        {
            var result = await mediator.Send(new GetUsersQuery(page, pageSize), ct);
            var items = result.Users
                .Select(u => new UserResponse(u.UserId, u.Email, u.Role, u.IsActive, u.CreatedAt))
                .ToList();
            return Results.Ok(new UserListResponse(items, result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        })
        .WithName("GetUsers")
        .Produces<UserListResponse>();

        group.MapPut("/{userId:guid}/role", async (
            Guid userId, UpdateUserRoleRequest request, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new UpdateUserRoleCommand(userId, request.Role), ct);
            return Results.NoContent();
        })
        .WithName("UpdateUserRole")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

        group.MapPut("/{userId:guid}/deactivate", async (
            Guid userId, IMediator mediator, CancellationToken ct) =>
        {
            await mediator.Send(new DeactivateUserCommand(userId), ct);
            return Results.NoContent();
        })
        .WithName("DeactivateUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound);

        return app;
    }
}
