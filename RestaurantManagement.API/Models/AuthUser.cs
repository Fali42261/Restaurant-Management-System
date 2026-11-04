namespace RestaurantManagement.API.Models;
public sealed class AuthUser{
 public long UserId{get;set;}
 public string FullName{get;set;}="";
 public string Email{get;set;}="";
 public string? CountryCode{get;set;}
 public string? MobileNumber{get;set;}
 public string PasswordHash{get;set;}="";
 public bool IsActive{get;set;}
 public bool IsEmailVerified{get;set;}
 public IReadOnlyList<string> Roles{get;set;}=[];
}