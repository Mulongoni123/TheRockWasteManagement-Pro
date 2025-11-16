using Microsoft.AspNetCore.Mvc;
using Google.Cloud.Firestore;
using TheRockWasteManagement.Models;
using System.Net.Mail;
using System.Net;

namespace TheRockWasteManagement.Controllers
{
    [Route("Customer")]
    public class CustomerController : Controller
    {
        private readonly FirestoreDb _firestoreDb;

        public CustomerController()
        {
            string projectId = "therockwastemanagement";
            _firestoreDb = FirestoreDb.Create(projectId);
        }

        [HttpGet("Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            if (!IsCustomerAuthenticated())
            {
                return RedirectToAction("Login", "Auth");
            }

            var uid = HttpContext.Session.GetString("uid");
            var email = HttpContext.Session.GetString("email");
            var emailVerified = HttpContext.Session.GetString("EmailVerified") == "True";

            // Get real-time statistics
            var stats = await GetCustomerStatistics(uid);
            var recentNotifications = await GetRecentNotifications(uid);

            ViewBag.UserId = uid;
            ViewBag.UserEmail = email;
            ViewBag.EmailVerified = emailVerified;
            ViewBag.Stats = stats;
            ViewBag.Notifications = recentNotifications;
            ViewBag.CustomerName = await GetCustomerNameFromFirestore(uid);

            return View();
        }

        private async Task<CustomerStats> GetCustomerStatistics(string customerId)
        {
            var snapshot = await _firestoreDb.Collection("bookings")
                .WhereEqualTo("CustomerId", customerId)
                .GetSnapshotAsync();

            var stats = new CustomerStats();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();
                var status = data.ContainsKey("Status") ? data["Status"].ToString().ToLower() : "";

                switch (status)
                {
                    case "completed":
                        stats.CompletedCount++;
                        break;
                    case "pending":
                        stats.PendingCount++;
                        break;
                    case "assigned":
                    case "in progress":
                        stats.InProgressCount++;
                        break;
                    case "approved":
                        stats.ScheduledCount++;
                        break;
                }

                stats.TotalBookings++;
            }

            return stats;
        }

        private async Task<List<Notification>> GetRecentNotifications(string customerId)
        {
            var notifications = new List<Notification>();

            try
            {
                var snapshot = await _firestoreDb.Collection("notifications")
                    .WhereEqualTo("CustomerId", customerId)
                    .OrderByDescending("CreatedAt")
                    .Limit(5)
                    .GetSnapshotAsync();

                foreach (var doc in snapshot.Documents)
                {
                    var data = doc.ToDictionary();
                    notifications.Add(new Notification
                    {
                        Id = doc.Id,
                        Title = data.ContainsKey("Title") ? data["Title"].ToString() : "Notification",
                        Message = data.ContainsKey("Message") ? data["Message"].ToString() : "",
                        Type = data.ContainsKey("Type") ? data["Type"].ToString() : "info",
                        CreatedAt = data.ContainsKey("CreatedAt") && data["CreatedAt"] is Timestamp ts ?
                                  ts.ToDateTime() : DateTime.Now,
                        IsRead = data.ContainsKey("IsRead") && data["IsRead"] is bool read ? read : false
                    });
                }
            }
            catch (Exception ex)
            {
                // If notifications collection doesn't exist, create sample notifications
                Console.WriteLine($"Notifications error: {ex.Message}");
                notifications = GetSampleNotifications();
            }

            return notifications;
        }

        private List<Notification> GetSampleNotifications()
        {
            return new List<Notification>
            {
                new Notification {
                    Title = "Welcome to DustbinPro!",
                    Message = "Thank you for choosing our waste management services.",
                    Type = "success",
                    CreatedAt = DateTime.Now.AddHours(-1)
                },
                new Notification {
                    Title = "Quick Tip",
                    Message = "Book your cleaning in advance for better slot availability.",
                    Type = "info",
                    CreatedAt = DateTime.Now.AddHours(-3)
                }
            };
        }

        [HttpPost("Logout")]
        [ValidateAntiForgeryToken]
        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            return RedirectToAction("Login", "Auth");
        }

        private bool IsCustomerAuthenticated()
        {
            return HttpContext.Session.GetString("IsLoggedIn") == "true" &&
                   HttpContext.Session.GetString("role") == "customer" &&
                   !string.IsNullOrEmpty(HttpContext.Session.GetString("uid"));
        }

        // BOOK CLEANING METHODS
        [HttpGet("BookCleaning")]
        public IActionResult BookCleaning()
        {
            var role = HttpContext.Session.GetString("role");
            if (role != "customer") return Redirect("/Auth/Login");

            var customerName = HttpContext.Session.GetString("customerName");
            if (string.IsNullOrEmpty(customerName))
            {
                customerName = GetCustomerNameFromDatabase().Result ?? "Customer";
            }

            ViewBag.CustomerName = customerName;
            return View();
        }

        [HttpPost("BookCleaning")]
        public async Task<IActionResult> BookCleaning(DateTime bookingDate, string bookingTime, string address,
            string serviceType, decimal estimatedPrice, string binSize = "", string carpetSize = "",
            string specialRequest = "")
        {
            var uid = HttpContext.Session.GetString("uid") ?? "unknown";
            var customerName = HttpContext.Session.GetString("customerName") ?? "Customer";

            // Check if customer already has approved/pending booking for this date
            var existingBookings = await _firestoreDb.Collection("bookings")
                .WhereEqualTo("CustomerId", uid)
                .WhereEqualTo("BookingDate", Timestamp.FromDateTime(bookingDate.ToUniversalTime()))
                .GetSnapshotAsync();

            bool hasActiveBooking = false;
            foreach (var doc in existingBookings.Documents)
            {
                var bookingData = doc.ToDictionary();
                if (bookingData.ContainsKey("Status"))
                {
                    var status = bookingData["Status"].ToString().ToLower();
                    if (status == "approved" || status == "pending" || status == "assigned")
                    {
                        hasActiveBooking = true;
                        break;
                    }
                }
            }

            if (hasActiveBooking)
            {
                ModelState.AddModelError(string.Empty, "You already have an active booking for this date. Please cancel your existing booking or choose a different date.");
                ViewBag.CustomerName = customerName;
                return View();
            }

            var booking = new Dictionary<string, object>
            {
                { "CustomerId", uid },
                { "CustomerName", customerName },
                { "BookingAddress", address },
                { "BookingDate", Timestamp.FromDateTime(bookingDate.ToUniversalTime()) },
                { "PreferredTime", bookingTime },
                { "Status", "pending" },
                { "ServiceType", serviceType },
                { "EstimatedPrice", (double)estimatedPrice },
                { "FinalPrice", 0.0 },
                { "IsPriceSet", false },
                { "PaymentStatus", "pending" },
                { "CreatedAt", Timestamp.FromDateTime(DateTime.UtcNow) },
                { "UpdatedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
            };

            if (!string.IsNullOrEmpty(binSize))
                booking.Add("BinSize", binSize);

            if (!string.IsNullOrEmpty(carpetSize))
                booking.Add("CarpetSize", carpetSize);

            if (!string.IsNullOrEmpty(specialRequest))
                booking.Add("SpecialRequest", specialRequest);

            await _firestoreDb.Collection("bookings").AddAsync(booking);
            await SendConfirmationEmail(customerName, bookingDate, bookingTime, address, serviceType);

            ViewBag.Success = true;
            ViewBag.BookingDate = bookingDate.ToShortDateString();
            ViewBag.Address = address;
            ViewBag.ServiceType = serviceType;
            ViewBag.CustomerName = customerName;

            return View();
        }

        // BOOKING HISTORY METHODS
        [HttpGet("BookingHistory")]
        public async Task<IActionResult> BookingHistory()
        {
            var uid = HttpContext.Session.GetString("uid");
            var role = HttpContext.Session.GetString("role");
            if (string.IsNullOrEmpty(uid) || role != "customer")
                return Redirect("/Auth/Login");

            var snapshot = await _firestoreDb.Collection("bookings")
                .WhereEqualTo("CustomerId", uid)
                .GetSnapshotAsync();

            var bookingList = new List<BookingViewModel>();

            foreach (var doc in snapshot.Documents)
            {
                var data = doc.ToDictionary();

                DateTime bookingDate = DateTime.Now;
                if (data.ContainsKey("BookingDate") && data["BookingDate"] is Timestamp ts)
                {
                    bookingDate = ts.ToDateTime();
                }

                double finalPrice = 0;
                if (data.ContainsKey("FinalPrice"))
                {
                    if (data["FinalPrice"] is double priceDouble)
                        finalPrice = priceDouble;
                    else if (data["FinalPrice"] is int priceInt)
                        finalPrice = priceInt;
                }

                bookingList.Add(new BookingViewModel
                {
                    BookingId = doc.Id,
                    Date = bookingDate,
                    Address = data.ContainsKey("BookingAddress") ? data["BookingAddress"].ToString() : "",
                    Status = data.ContainsKey("Status") ? data["Status"].ToString() : "Unknown",
                    FinalPrice = finalPrice,
                    ServiceType = data.ContainsKey("ServiceType") ? data["ServiceType"].ToString() : "Unknown",
                    PaymentStatus = data.ContainsKey("PaymentStatus") ? data["PaymentStatus"].ToString() : "pending"
                });
            }

            return View(bookingList);
        }

        [HttpPost("CancelBooking")]
        public async Task<IActionResult> CancelBooking(string bookingId)
        {
            var uid = HttpContext.Session.GetString("uid");
            var role = HttpContext.Session.GetString("role");

            if (string.IsNullOrEmpty(uid) || role != "customer")
                return Redirect("/Auth/Login");

            if (string.IsNullOrEmpty(bookingId))
                return BadRequest("Invalid booking ID.");

            var bookingRef = _firestoreDb.Collection("bookings").Document(bookingId);
            var snapshot = await bookingRef.GetSnapshotAsync();

            if (!snapshot.Exists)
                return NotFound("Booking not found.");

            var data = snapshot.ToDictionary();
            if (data["CustomerId"].ToString() != uid)
                return Forbid("You are not authorized to cancel this booking.");

            await bookingRef.UpdateAsync(new Dictionary<string, object>
            {
                { "Status", "cancelled" }
            });

            TempData["SuccessMessage"] = "Your booking has been successfully cancelled.";

            if (data.ContainsKey("CustomerEmail"))
            {
                string customerEmail = data["CustomerEmail"].ToString();
                await SendCancellationEmail(customerEmail, data);
            }

            return RedirectToAction("BookingHistory");
        }

        // PAYMENT METHODS
        [HttpGet("MakePayment")]
        public async Task<IActionResult> MakePayment()
        {
            var uid = HttpContext.Session.GetString("uid");
            if (string.IsNullOrEmpty(uid))
                return Redirect("/Auth/Login");

            var pendingBookingsSnapshot = await _firestoreDb.Collection("bookings")
                .WhereEqualTo("CustomerId", uid)
                .WhereEqualTo("IsPriceSet", true)
                .WhereEqualTo("PaymentStatus", "pending")
                .WhereIn("Status", new[] { "approved", "assigned" })
                .GetSnapshotAsync();

            var pendingBookings = new List<dynamic>();
            foreach (var doc in pendingBookingsSnapshot.Documents)
            {
                var data = doc.ToDictionary();

                double finalPrice = 0;
                if (data.ContainsKey("FinalPrice"))
                {
                    if (data["FinalPrice"] is double priceDouble)
                        finalPrice = priceDouble;
                    else if (data["FinalPrice"] is int priceInt)
                        finalPrice = priceInt;
                }

                pendingBookings.Add(new
                {
                    BookingId = doc.Id,
                    ServiceType = data.ContainsKey("ServiceType") ? data["ServiceType"].ToString() : "Unknown",
                    Address = data.ContainsKey("BookingAddress") ? data["BookingAddress"].ToString() : "",
                    BookingDate = data.ContainsKey("BookingDate") && data["BookingDate"] is Timestamp ts ? ts.ToDateTime() : DateTime.Now,
                    TimeSlot = data.ContainsKey("PreferredTime") ? data["PreferredTime"].ToString() : "",
                    FinalPrice = finalPrice
                });
            }

            ViewBag.PendingBookings = pendingBookings;
            return View();
        }

        [HttpPost("MakePayment")]
        public async Task<IActionResult> MakePayment(string bookingId, double amount, string paymentMethod, string reference, string description, string serviceType)
        {
            var uid = HttpContext.Session.GetString("uid");

            string customerName = "Unknown Customer";
            try
            {
                var userDoc = await _firestoreDb.Collection("users").Document(uid).GetSnapshotAsync();
                if (userDoc.Exists)
                {
                    var userData = userDoc.ToDictionary();

                    string firstName = userData.ContainsKey("firstName") && userData["firstName"] != null
                        ? userData["firstName"].ToString() : "";

                    string lastName = userData.ContainsKey("lastName") && userData["lastName"] != null
                        ? userData["lastName"].ToString() : "";

                    customerName = $"{firstName} {lastName}".Trim();

                    if (string.IsNullOrEmpty(customerName) && userData.ContainsKey("name") && userData["name"] != null)
                        customerName = userData["name"].ToString();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting customer name: {ex.Message}");
            }

            try
            {
                var paymentData = new Dictionary<string, object>
                {
                    { "CustomerId", uid },
                    { "CustomerName", customerName },
                    { "Amount", amount },
                    { "PaymentMethod", paymentMethod },
                    { "Reference", reference ?? "" },
                    { "Description", description ?? $"Payment for {serviceType} cleaning" },
                    { "PaymentDate", Timestamp.FromDateTime(DateTime.UtcNow) },
                    { "Status", "completed" }
                };

                if (!string.IsNullOrEmpty(bookingId))
                {
                    paymentData["BookingId"] = bookingId;
                    await _firestoreDb.Collection("bookings").Document(bookingId).UpdateAsync(new Dictionary<string, object>
                    {
                        { "PaymentStatus", "paid" },
                        { "UpdatedAt", DateTime.UtcNow }
                    });
                }

                await _firestoreDb.Collection("payments").AddAsync(paymentData);

                ViewBag.PaymentSuccess = true;
                ViewBag.Amount = amount;

                return await MakePayment();
            }
            catch (Exception ex)
            {
                ViewBag.Error = $"Payment failed: {ex.Message}";
                return await MakePayment();
            }
        }

        // PROFILE, SUPPORT, NOTIFICATIONS
        [HttpGet("Profile")]
        public async Task<IActionResult> Profile()
        {
            if (!IsCustomerAuthenticated())
                return RedirectToAction("Login", "Auth");

            var uid = HttpContext.Session.GetString("uid");
            var userDoc = await _firestoreDb.Collection("users").Document(uid).GetSnapshotAsync();

            if (userDoc.Exists)
            {
                var userData = userDoc.ToDictionary();
                var profile = new CustomerProfile
                {
                    FirstName = userData.ContainsKey("firstName") ? userData["firstName"].ToString() : "",
                    LastName = userData.ContainsKey("lastName") ? userData["lastName"].ToString() : "",
                    Email = userData.ContainsKey("email") ? userData["email"].ToString() : "",
                    Phone = userData.ContainsKey("phone") ? userData["phone"].ToString() : "",
                    Address = userData.ContainsKey("address") ? userData["address"].ToString() : ""
                };
                return View(profile);
            }

            return View(new CustomerProfile());
        }

        [HttpPost("UpdateProfile")]
        public async Task<IActionResult> UpdateProfile(CustomerProfile profile)
        {
            if (!IsCustomerAuthenticated())
                return RedirectToAction("Login", "Auth");

            var uid = HttpContext.Session.GetString("uid");

            try
            {
                var updateData = new Dictionary<string, object>
                {
                    { "firstName", profile.FirstName },
                    { "lastName", profile.LastName },
                    { "phone", profile.Phone ?? "" },
                    { "address", profile.Address ?? "" },
                    { "updatedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
                };

                await _firestoreDb.Collection("users").Document(uid).UpdateAsync(updateData);

                HttpContext.Session.SetString("customerName", $"{profile.FirstName} {profile.LastName}");

                TempData["SuccessMessage"] = "Profile updated successfully!";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error updating profile: {ex.Message}";
            }

            return RedirectToAction("Profile");
        }

        [HttpGet("Support")]
        public IActionResult Support()
        {
            if (!IsCustomerAuthenticated())
                return RedirectToAction("Login", "Auth");

            return View();
        }

        [HttpPost("SubmitSupport")]
        public async Task<IActionResult> SubmitSupport(string subject, string message, string priority)
        {
            if (!IsCustomerAuthenticated())
                return RedirectToAction("Login", "Auth");

            var uid = HttpContext.Session.GetString("uid");
            var customerName = await GetCustomerNameFromFirestore(uid);

            try
            {
                var supportData = new Dictionary<string, object>
                {
                    { "CustomerId", uid },
                    { "CustomerName", customerName },
                    { "Subject", subject },
                    { "Message", message },
                    { "Priority", priority },
                    { "Status", "open" },
                    { "CreatedAt", Timestamp.FromDateTime(DateTime.UtcNow) },
                    { "UpdatedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
                };

                await _firestoreDb.Collection("support_tickets").AddAsync(supportData);

                var notificationData = new Dictionary<string, object>
                {
                    { "CustomerId", uid },
                    { "Title", "Support Ticket Created" },
                    { "Message", $"We've received your support request: {subject}" },
                    { "Type", "info" },
                    { "IsRead", false },
                    { "CreatedAt", Timestamp.FromDateTime(DateTime.UtcNow) }
                };
                await _firestoreDb.Collection("notifications").AddAsync(notificationData);

                TempData["SuccessMessage"] = "Support ticket submitted successfully! We'll get back to you soon.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error submitting support ticket: {ex.Message}";
            }

            return RedirectToAction("Support");
        }

        [HttpGet("Notifications")]
        public async Task<IActionResult> Notifications()
        {
            if (!IsCustomerAuthenticated())
                return RedirectToAction("Login", "Auth");

            var uid = HttpContext.Session.GetString("uid");
            var notifications = await GetRecentNotifications(uid);

            return View(notifications);
        }

        [HttpPost("MarkNotificationRead")]
        public async Task<IActionResult> MarkNotificationRead(string notificationId)
        {
            if (!IsCustomerAuthenticated())
                return Json(new { success = false });

            try
            {
                await _firestoreDb.Collection("notifications").Document(notificationId)
                    .UpdateAsync("IsRead", true);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = ex.Message });
            }
        }

        // HELPER METHODS
        private async Task<string> GetCustomerNameFromDatabase()
        {
            var uid = HttpContext.Session.GetString("uid");
            if (string.IsNullOrEmpty(uid)) return null;

            try
            {
                var userDoc = await _firestoreDb.Collection("users").Document(uid).GetSnapshotAsync();
                if (userDoc.Exists)
                {
                    var userData = userDoc.ToDictionary();
                    if (userData.ContainsKey("name"))
                    {
                        var name = userData["name"].ToString();
                        HttpContext.Session.SetString("customerName", name);
                        return name;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting customer name: {ex.Message}");
            }

            return null;
        }

        private async Task<string> GetCustomerNameFromFirestore(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return "Customer";

            try
            {
                var userDoc = await _firestoreDb.Collection("users").Document(uid).GetSnapshotAsync();
                if (userDoc.Exists)
                {
                    var userData = userDoc.ToDictionary();

                    string firstName = userData.ContainsKey("firstName") && userData["firstName"] != null
                        ? userData["firstName"].ToString() : "";
                    string lastName = userData.ContainsKey("lastName") && userData["lastName"] != null
                        ? userData["lastName"].ToString() : "";

                    string fullName = $"{firstName} {lastName}".Trim();
                    return string.IsNullOrEmpty(fullName) ? "Customer" : fullName;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error getting customer name: {ex.Message}");
            }

            return "Customer";
        }

        [HttpGet("CheckActiveBooking")]
        public async Task<IActionResult> CheckActiveBooking(DateTime date)
        {
            var uid = HttpContext.Session.GetString("uid");
            if (string.IsNullOrEmpty(uid))
                return Json(new { hasActiveBooking = false });

            try
            {
                var existingBookings = await _firestoreDb.Collection("bookings")
                    .WhereEqualTo("CustomerId", uid)
                    .WhereEqualTo("BookingDate", Timestamp.FromDateTime(date.ToUniversalTime()))
                    .GetSnapshotAsync();

                bool hasActiveBooking = false;
                foreach (var doc in existingBookings.Documents)
                {
                    var bookingData = doc.ToDictionary();
                    if (bookingData.ContainsKey("Status"))
                    {
                        var status = bookingData["Status"].ToString().ToLower();
                        if (status == "approved" || status == "pending" || status == "assigned")
                        {
                            hasActiveBooking = true;
                            break;
                        }
                    }
                }

                return Json(new { hasActiveBooking });
            }
            catch (Exception ex)
            {
                return Json(new { hasActiveBooking = false, error = ex.Message });
            }
        }

        private async Task SendConfirmationEmail(string customerName, DateTime date, string time, string address, string serviceType)
        {
            // Your email sending logic here
            // This is just a placeholder
            await Task.CompletedTask;
        }

        private async Task SendCancellationEmail(string toEmail, Dictionary<string, object> data)
        {
            // Your email sending logic here
            // This is just a placeholder
            await Task.CompletedTask;
        }
    }

    // HELPER CLASSES
    public class CustomerStats
    {
        public int TotalBookings { get; set; }
        public int ScheduledCount { get; set; }
        public int CompletedCount { get; set; }
        public int PendingCount { get; set; }
        public int InProgressCount { get; set; }
    }

    public class Notification
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Message { get; set; }
        public string Type { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsRead { get; set; }
    }

    public class CustomerProfile
    {
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string Email { get; set; }
        public string Phone { get; set; }
        public string Address { get; set; }
    }
}