using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using RestaurantManagement.API.Infrastructure;
using RestaurantManagement.API.Models;

namespace RestaurantManagement.API.Controllers;

[Authorize(Roles="Owner,Manager"), Route("api/v1/users")]
public sealed class UsersController(SqlDataAccess db, IPasswordHasher<string> hasher) : ApiControllerBase(db)
{
    [HttpGet("staff")]
    public async Task<IActionResult> Staff([FromQuery]int pageNumber=1,[FromQuery]int pageSize=25,[FromQuery]string? search=null)
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        var page=Math.Max(1,pageNumber); var size=Math.Clamp(pageSize,1,100);
        var rows=await Db.QueryProcedureAsync("dbo.usp_Staff_ListPaged",P("@PageNumber",page),P("@PageSize",size+1),P("@Search",string.IsNullOrWhiteSpace(search)?null:search.Trim()),P("@RestaurantId",CurrentRestaurantId));
        var hasMore=rows.Count>size; if(hasMore) rows.RemoveAt(size);
        return Success(new {items=rows,pageNumber=page,pageSize=size,hasMore});
    }

    [HttpPost("staff")]
    public async Task<IActionResult> CreateStaff(OwnerRegistration r)
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        if(string.IsNullOrWhiteSpace(r.FullName)||r.FullName.Trim().Length>150||!System.Net.Mail.MailAddress.TryCreate(r.Email,out _)||
           string.IsNullOrWhiteSpace(r.MobileNumber)||r.MobileNumber.Trim().Length<10||!Strong(r.Password))
            return BadRequest(new {success=false,message="Enter valid staff details and a strong password."});
        var email=r.Email.Trim().ToLowerInvariant(); var mobile=r.MobileNumber.Trim();
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Users WHERE IsDeleted=0 AND (Email=@Email OR MobileNumber=@Mobile)",P("@Email",email),P("@Mobile",mobile));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="Email or mobile number is already registered."});
        var id=Guid.NewGuid(); var hash=hasher.HashPassword(id.ToString(),r.Password);
        await Db.ExecuteAsync(@"SET XACT_ABORT ON;
BEGIN TRANSACTION;
BEGIN TRY
 INSERT dbo.Users(UserId,FullName,Email,MobileNumber,PasswordHash,Role,IsActive,CreatedAt) VALUES(@Id,@Name,@Email,@Mobile,@Hash,'Staff',1,SYSUTCDATETIME());
 INSERT dbo.UserRestaurants(UserId,RestaurantId,Role,IsActive,CreatedAt) VALUES(@Id,@RestaurantId,'Staff',1,SYSUTCDATETIME());
 COMMIT TRANSACTION;
END TRY
BEGIN CATCH
 IF @@TRANCOUNT>0 ROLLBACK TRANSACTION;
 THROW;
END CATCH",P("@Id",id),P("@Name",r.FullName.Trim()),P("@Email",email),P("@Mobile",mobile),P("@Hash",hash),P("@RestaurantId",CurrentRestaurantId));
        return CreatedResult(new {userId=id,role="Staff",restaurantId=CurrentRestaurantId});
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> UpdateStaff(Guid id,UpdateStaffRequest r)
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        if(r.Role is not ("Manager" or "Staff")||string.IsNullOrWhiteSpace(r.FullName)||!System.Net.Mail.MailAddress.TryCreate(r.Email,out _)||string.IsNullOrWhiteSpace(r.MobileNumber))
            return BadRequest(new {success=false,message="Invalid staff details or role."});
        if(User.IsInRole("Manager")&&r.Role=="Manager") return Forbid();
        var duplicate=await Db.ScalarAsync("SELECT COUNT(1) FROM dbo.Users WHERE IsDeleted=0 AND UserId<>@Id AND (Email=@Email OR MobileNumber=@Mobile)",P("@Id",id),P("@Email",r.Email.Trim().ToLowerInvariant()),P("@Mobile",r.MobileNumber.Trim()));
        if(Convert.ToInt32(duplicate)>0) return Conflict(new {success=false,message="Email or mobile number is already registered."});
        var actorRole=User.IsInRole("Owner")?"Owner":"Manager";
        var n=await Db.ExecuteAsync("UPDATE dbo.Users SET FullName=@Name,Email=@Email,MobileNumber=@Mobile,IsActive=@Active,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id AND Role IN('Manager','Staff') AND EXISTS(SELECT 1 FROM dbo.UserRestaurants WHERE UserId=@Id AND RestaurantId=@RestaurantId AND IsActive=1 AND (@ActorRole='Owner' OR Role='Staff')); UPDATE dbo.UserRestaurants SET Role=@Role WHERE UserId=@Id AND RestaurantId=@RestaurantId AND IsActive=1 AND (@ActorRole='Owner' OR Role='Staff')",
            P("@Id",id),P("@Name",r.FullName.Trim()),P("@Email",r.Email.Trim().ToLowerInvariant()),P("@Mobile",r.MobileNumber.Trim()),P("@Active",r.IsActive),P("@Role",r.Role),P("@RestaurantId",CurrentRestaurantId),P("@ActorRole",actorRole));
        return n==0?NotFound():Success(null,"Staff account updated.");
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> DeleteStaff(Guid id)
    {
        if(CurrentRestaurantId==Guid.Empty) return BadRequest(new {success=false,message="Select a restaurant first."});
        var actorRole=User.IsInRole("Owner")?"Owner":"Manager";
        var n=await Db.ExecuteAsync("UPDATE dbo.UserRestaurants SET IsActive=0 WHERE UserId=@Id AND RestaurantId=@RestaurantId AND IsActive=1 AND (@ActorRole='Owner' OR Role='Staff'); UPDATE dbo.Users SET IsActive=0,IsDeleted=1,UpdatedAt=SYSUTCDATETIME() WHERE UserId=@Id AND Role IN('Manager','Staff') AND EXISTS(SELECT 1 FROM dbo.UserRestaurants WHERE UserId=@Id AND RestaurantId=@RestaurantId AND Role IN('Manager','Staff') AND (@ActorRole='Owner' OR Role='Staff')) AND NOT EXISTS(SELECT 1 FROM dbo.UserRestaurants WHERE UserId=@Id AND IsActive=1)",
            P("@Id",id),P("@RestaurantId",CurrentRestaurantId),P("@ActorRole",actorRole));
        return n==0?NotFound():Success(null,"Staff access removed.");
    }

    private static bool Strong(string? value)=>!string.IsNullOrWhiteSpace(value)&&value.Length>=8&&value.Any(char.IsUpper)&&value.Any(char.IsLower)&&value.Any(char.IsDigit)&&value.Any(c=>!char.IsLetterOrDigit(c));
}
