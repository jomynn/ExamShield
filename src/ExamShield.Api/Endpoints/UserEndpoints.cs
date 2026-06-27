using ExamShield.Api.Contracts;
using ExamShield.Application.Commands.DeactivateUser;
using ExamShield.Application.Commands.ReactivateUser;
using ExamShield.Application.Commands.UpdateUserProfile;
using ExamShield.Domain.Exceptions;
using ExamShield.Application.Commands.UpdateUserRole;
using ExamShield.Application.Queries.ExportUsers;
using ExamShield.Application.Queries.GetUserById;
using ExamShield.Application.Queries.GetUsers;
using MediatR;

namespace ExamShield.Api.Endpoints;

public static class UserEndpoints
{
    public static IEndpointRouteBuilder MapUserEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/users").WithTags("Users").RequireAuthorization("Administrator");

        group.MapGet("/", async (
            IMediator mediator, CancellationToken ct,
            int page = 1, int pageSize = 50,
            string? search = null, string? role = null,
            bool? isActive = null) =>
        {
            var result = await mediator.Send(new GetUsersQuery(page, pageSize, search, role, isActive), ct);
            var items = result.Users
                .Select(u => new UserResponse(u.UserId, u.Email, u.Role, u.IsActive, u.CreatedAt))
                .ToList();
            return Results.Ok(new UserListResponse(items, result.TotalCount, result.Page, result.PageSize, result.TotalPages));
        })
        .WithName("GetUsers")
        .Produces<UserListResponse>();

        group.MapGet("/export", async (
            IMediator mediator, CancellationToken ct,
            string? search = null, string? role = null) =>
        {
            var result = await mediator.Send(new ExportUsersQuery(search, role), ct);
            return Results.File(
                System.Text.Encoding.UTF8.GetBytes(result.Csv),
                "text/csv",
                result.Filename);
        })
        .WithName("ExportUsers")
        .RequireAuthorization("Auditor")
        .Produces(StatusCodes.Status200OK, contentType: "text/csv");

        group.MapGet("/{userId:guid}", async (Guid userId, IMediator mediator, CancellationToken ct) =>
        {
            var result = await mediator.Send(new GetUserByIdQuery(userId), ct);
            if (result is null) return Results.NotFound();
            return Results.Ok(new UserDetailResponse(
                result.UserId, result.Email, result.DisplayName,
                result.Role, result.IsActive, result.MfaEnabled, result.CreatedAt));
        })
        .WithName("GetUserById")
        .Produces<UserDetailResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound);

        group.MapPut("/{userId:guid}", async (
            Guid userId, UpdateUserProfileRequest request, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                await mediator.Send(new UpdateUserProfileCommand(userId, request.DisplayName), ct);
                return Results.NoContent();
            }
            catch (KeyNotFoundException)           { return Results.NotFound(); }
            catch (ArgumentOutOfRangeException e)  { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e)            { return Results.BadRequest(new { error = e.Message }); }
        })
        .WithName("UpdateUserProfile")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status400BadRequest);

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

        group.MapPut("/{userId:guid}/activate", async (
            Guid userId, IMediator mediator, CancellationToken ct) =>
        {
            try
            {
                await mediator.Send(new ReactivateUserCommand(userId), ct);
                return Results.NoContent();
            }
            catch (UserNotFoundException)
            {
                return Results.NotFound();
            }
            catch (InvalidOperationException e)
            {
                return Results.UnprocessableEntity(new { error = e.Message });
            }
        })
        .WithName("ReactivateUser")
        .Produces(StatusCodes.Status204NoContent)
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status422UnprocessableEntity);

        return app;
    }
}
