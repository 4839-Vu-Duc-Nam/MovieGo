using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MovieGoApp.Data;
using MovieGoApp.Models;
using System.Security.Claims;

namespace MovieGoApp.Controllers;

public sealed class AccountController(AuthenticationStore store, MovieGoStore movieStore) : Controller
{
    [AllowAnonymous]
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction(nameof(MyBookings));
        return View(new LoginForm { ReturnUrl = returnUrl });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(form);

        var account = await store.AuthenticateAsync(form.Email, form.Password, cancellationToken);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Email hoặc mật khẩu không chính xác.");
            return View(form);
        }

        await SignInAsync(account);
        if (Url.IsLocalUrl(form.ReturnUrl))
            return LocalRedirect(form.ReturnUrl!);
        return account.Role == AuthenticationStore.AdminRole
            ? RedirectToAction("Index", "Admin")
            : RedirectToAction(nameof(MyBookings));
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register() =>
        User.Identity?.IsAuthenticated == true ? RedirectToAction(nameof(MyBookings)) : View(new RegisterForm());

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterForm form, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(form);

        try
        {
            await store.RegisterCustomerAsync(form, cancellationToken);
            var account = await store.AuthenticateAsync(form.Email, form.Password, cancellationToken)
                ?? throw new InvalidOperationException("The newly registered account could not be loaded.");
            await SignInAsync(account);
            return RedirectToAction(nameof(MyBookings));
        }
        catch (ArgumentException ex)
        {
            ModelState.AddModelError(nameof(form.Email), ex.Message);
            return View(form);
        }
    }

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> MyBookings(CancellationToken cancellationToken)
    {
        var customerId = User.FindFirstValue(AuthenticationStore.CustomerIdClaim);
        if (string.IsNullOrWhiteSpace(customerId))
            return User.IsInRole(AuthenticationStore.AdminRole) ? RedirectToAction("Index", "Admin") : Forbid();

        var invoices = await movieStore.GetCustomerInvoicesAsync(customerId, cancellationToken);
        return View(invoices);
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult AccessDenied() => View();

    private async Task SignInAsync(AuthenticatedAccount account)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.AccountId),
            new(ClaimTypes.Name, account.DisplayName),
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.Role, account.Role)
        };
        if (!string.IsNullOrWhiteSpace(account.CustomerId))
            claims.Add(new Claim(AuthenticationStore.CustomerIdClaim, account.CustomerId));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity));
    }
}
