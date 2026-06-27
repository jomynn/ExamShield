using ExamShield.Domain.Entities;
using ExamShield.Domain.Enums;
using ExamShield.Domain.Exceptions;
using ExamShield.Domain.Interfaces;
using ExamShield.Domain.ValueObjects;
using MediatR;

namespace ExamShield.Application.Commands.CreateUser;

public sealed class CreateUserCommandHandler : IRequestHandler<CreateUserCommand, CreateUserResult>
{
    private readonly IUserRepository _users;
    private readonly IPasswordHasher _hasher;
    private readonly IAuditLogRepository _auditLog;

    public CreateUserCommandHandler(
        IUserRepository users, IPasswordHasher hasher, IAuditLogRepository auditLog)
    {
        _users = users;
        _hasher = hasher;
        _auditLog = auditLog;
    }

    public async Task<CreateUserResult> Handle(CreateUserCommand command, CancellationToken ct)
    {
        var email    = new Email(command.Email);
        var password = new Password(command.Password);

        var existing = await _users.FindByEmailAsync(email, ct);
        if (existing is not null)
            throw new UserAlreadyExistsException(command.Email);

        var user = User.Create(email, _hasher.Hash(password.Value), command.Role);

        await _users.AddAsync(user, ct);
        await _auditLog.AppendAsync(AuditLog.Record(AuditAction.UserCreated), ct);

        return new CreateUserResult(user.Id.Value);
    }
}
