
namespace UsersAPI.Events {
    public record UserCreatedEvent(Guid UserId, string Email);
}
