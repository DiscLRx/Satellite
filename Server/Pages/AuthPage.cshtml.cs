using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace Server.Pages;

public class AuthPage : PageModel
{
    [BindProperty(SupportsGet = true,  Name = "original")]
    public string OriginalPath { get; set; } = string.Empty;

    public void OnGet()
    {
    }
}