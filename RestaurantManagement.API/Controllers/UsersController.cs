using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager"), Route("api/v1/users")]
public sealed class UsersController(SqlDataAccess db, IPasswordHasher<string> hasher) : ApiControllerBase(db)
{
    [HttpGet("staff")] public async Task<IActionResult> Staff() => Success(await Db.QueryAsync("SELECT UserId,FullName,Email,MobileNumber,Role,IsActive,CreatedAt FROM dbo.Users WHERE Role IN('Manager','Staff') AND IsDeleted=0 ORDER BY FullName"));
    [HttpPost("staff")] public async Task<IActionResult> CreateStaff(OwnerRegistration r)
    {
        var id=Guid.NewGuid(); var hash=hasher.HashPassword(id.ToString(),r.Password);
        await Db.ExecuteAsync("INSERT dbo.Users(UserId,FullName,Email,MobileNumber,PasswordHash,Role,IsActive,CreatedAt) VALUES(@Id,@Name,@Email,@Mobile,@Hash,'Staff',1,SYSUTCDATETIME())",P("@Id",id),P("@Name",r.FullName),P("@Email",r.Email),P("@Mobile",r.MobileNumber),P("@Hash",hash));
        return CreatedResult(new { userId=id,role="Staff" });
    }
    [HttpPut("{id:guid}")] public async Task<IActionResult> UpdateStaff(Guid id, UpdateStaffRequest r)
    {
        if(r.Role is not ("Manager" or "Staff")) return BadRequest(new {success=false,message="Only Manager or Staff roles can be assigned here."});
        var n=await Db.ExecuteAsync("UPDATE dbo.Users SET FullName=@Name,Email=@Email,MobileNumber=@Mobile,Role=@Role,IsActive=@Active,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id AND Role IN('Manager','Staff')",P("@Id",id),P("@Name",r.FullName),P("@Email",r.Email),P("@Mobile",r.MobileNumber),P("@Role",r.Role),P("@Active",r.IsActive));
        return n==0?NotFound():Success(null,"Staff account updated.");
    }
    [HttpDelete("{id:guid}")] public async Task<IActionResult> DeleteStaff(Guid id) { var n=await Db.ExecuteAsync("UPDATE dbo.Users SET IsDeleted=1,IsActive=0 WHERE UserId=@Id AND Role IN('Manager','Staff')",P("@Id",id)); return n==0?NotFound():Success(null,"Staff account deactivated."); }
}