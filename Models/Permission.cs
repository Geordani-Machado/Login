using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Login.Models;

public class Permission
{
    [Key]
    public int Id { get; set; }

    [Required]
    public string Name { get; set; }
}

public class UserPermission
{
    [Key]
    [Column(Order = 1)]
    public int UserId { get; set; }

    [Key]
    [Column(Order = 2)]
    public int PermissionId { get; set; }

    public User User { get; set; }
    public Permission Permission { get; set; }
}
