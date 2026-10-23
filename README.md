# MovieGo

ASP.NET Core MVC ticket-booking sample for the SQL Server schema in `MovieGo.sql`.

## Requirements

- .NET 9 SDK
- SQL Server

## Run locally

1. For a fresh install, run `MovieGo.sql` from this project directory once in SQL Server Management Studio or with `sqlcmd`. It creates the `MovieGo` database and sample data, including 100 demo seats in each sample room and six additional films. Do not run the full script against a database that already has the MovieGo tables.
   - For a database created before the cinema seat-rule update, back it up and run `MovieGo_SeatRules_Migration.sql` once instead. The upgrade script updates the existing schema and sample rooms without recreating the database.
   - For an existing database, run `MovieGo_Auth_Migration.sql` once to create the account table before using sign-up or sign-in.
   - To add the six additional sample films to an existing database without recreating it, run `MovieGo_MoreMovies.sql` once after the schema is up to date. It can safely be rerun; it adds future sample showtimes in available room slots. Its film titles, release dates, and runtimes are based on the public references below; demo age ratings and showtimes are illustrative.
2. If SQL Server is not `localhost`, update `ConnectionStrings:MovieGo` in `appsettings.json`, or override it with the `ConnectionStrings__MovieGo` environment variable.
3. From this directory, run:

   ```powershell
   dotnet restore
   dotnet run
   ```

4. Open the local URL printed by `dotnet run`.
5. To provision the first administrator on an existing database, set `MovieGoAdmin__Email` and `MovieGoAdmin__Password` before the first app start (password must be at least 12 characters). For example, in PowerShell:

   ```powershell
   $env:MovieGoAdmin__Email = "admin@example.com"
   $adminPassword = Read-Host "Choose an admin password" -AsSecureString
   $env:MovieGoAdmin__Password = [System.Net.NetworkCredential]::new("", $adminPassword).Password
   dotnet run
   Remove-Variable adminPassword
   Remove-Item Env:MovieGoAdmin__Email
   Remove-Item Env:MovieGoAdmin__Password
   ```

   The initial admin password is stored as a password hash. After the first start provisions the account, stop the app with Ctrl+C, clear the two environment variables, and start it again. The app does not provide public admin registration or a built-in default password. Existing databases must have `MovieGo_Auth_Migration.sql` applied before starting with bootstrap-admin settings. Customer registration is available from the navigation bar.

The sample includes:

- Dashboard, movie catalog, and upcoming showtimes.
- Cookie-based sign-in and registration: public registration creates customer-only accounts, while administrators are provisioned out of band and never through the public form.
- Role-protected administration, counter sales, revenue, and ticket-operation routes. Customers can book only under their own account and review/pay/cancel only their own invoices.
- Cinema-style seat map grouped by row, with Standard, VIP, Couple, unavailable, and selected-seat states, per-seat live prices, and transactional ten-minute holds.
- Seat-selection rules: at most eight seats per transaction, adjacent seats by default, split seats only when no suitable adjacent block remains and the customer opts in, and validation that prevents a single empty seat gap.
- Single-seat gap validation also checks row ends (treated as aisle edges) and prevents stranding one seat beside an already selected/booked seat, including at the start or end of a row.
- Administrators can mark an individual low-demand showtime to temporarily block rows A-B and the two end seats on every row; seat restrictions apply at both online and counter checkout.
- Couple seats are the final row, paired as seats 1-2, 3-4, etc., and must be booked as one pair. Hold expiration is shown as a countdown and enforced by SQL Server.
- Couple-seat tickets cannot currently be returned or exchanged individually; those operations require a paired-ticket workflow.
- Invoice review and simulated confirmation through the SQL payment procedure.
- Customer cancellation of an unpaid invoice, with seats released and cancellation history retained.
- Automatic processing of expired seat holds once per minute while the web app is running.
- Staff ticket operations for returns and exchanges, using the SQL business procedures and preserving ticket history.
- Revenue reports grouped by date, movie, showtime, or employee.
- Back-office create/update screens for movies and genres, rooms and seats, showtimes, customers, and employees.
- Bulk seat generation by room, row labels, and seat count; already-existing positions are skipped, rooms are capped at 150 seats, and each showtime has administrator-entered Standard, VIP, and per-seat Couple prices.
- Theater-initiated showtime cancellation through the SQL business procedure, with affected holds/sold tickets cancelled and refund history retained.
- Counter sales with employee, optional customer, channel-compatible payment method, cinema-style seat map, and atomic paid invoice/ticket creation.

Payment confirmation is simulated: no bank, wallet, or payment gateway is connected. The sample is intended for coursework and does not include user authentication or production deployment hardening. Do not expose the staff operation, counter-sale, or administration routes to the public internet without adding authentication and authorization. Existing records can be updated by entering their ID in the administration form; leave the ID blank to create a record. Preserve referenced records and use inactive/cancelled statuses where available.

Additional film metadata references:

- [Avatar: Fire and Ash](https://en.wikipedia.org/wiki/Avatar:_Fire_and_Ash)
- [Zootopia 2](https://en.wikipedia.org/wiki/Zootopia_2)
- [A Minecraft Movie](https://en.wikipedia.org/wiki/Minecraft_(film))
- [How to Train Your Dragon (2025)](https://en.wikipedia.org/wiki/How_to_Train_Your_Dragon_(2025_film))
- [Superman (2025)](https://en.wikipedia.org/wiki/Superman_(2025_film))
- [F1](https://en.wikipedia.org/wiki/F1_(film))
