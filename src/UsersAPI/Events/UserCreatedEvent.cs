
//namespace UsersAPI.Events {
//    public record UserCreatedEvent(Guid UserId, string Email);
//}

//namespace FCG.Events;
namespace UsersAPI.Events;
public record UserCreatedEvent(Guid UserId, string Email, string? Nome, bool Ativo);
