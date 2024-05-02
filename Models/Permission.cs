using Login.Models;

public class Permission
{
    public int Id { get; set; }
    public string Name { get; set; }
}

public class UserPermission
{
    public int UserId { get; set; }

    public int PermissionId { get; set; }

    public User User { get; set; }
    public Permission Permission { get; set; }
}
