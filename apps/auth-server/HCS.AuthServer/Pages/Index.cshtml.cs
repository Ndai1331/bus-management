using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace HCS.AuthServer.Pages;

public class IndexModel(IConfiguration configuration) : PageModel
{
    public IActionResult OnGet()
    {
        var clientUrl = configuration["App:ClientUrl"];
        if (string.IsNullOrWhiteSpace(clientUrl))
        {
            clientUrl = "https://hcs.localhost/";
        }

        return Redirect(clientUrl.TrimEnd('/') + "/");
    }
}
