The Rock Waste Management System 🚀
A comprehensive, cloud-based waste management platform built with ASP.NET Core that streamlines the entire service lifecycle from booking to completion.

📋 Project Overview
The Rock Waste Management System is a full-stack web application designed to modernize waste management services. It provides a seamless experience for customers, workers, and administrators with real-time features and robust functionality.

✨ Features
👥 Multi-Role System
Customers: Book services, track progress, make payments

Workers: Manage assignments, update task status

Admins: Oversee operations, assign workers, analytics

🎯 Core Functionalities
🔐 Secure Authentication with role-based access

📅 Smart Booking System with conflict prevention

💳 Integrated Payment Processing

🔔 Real-time Notifications

📊 Live Dashboard with statistics

🎫 Support Ticket System

📱 Responsive Design

🛠️ Technology Stack
Layer	Technology
Frontend	ASP.NET Core MVC, Bootstrap 5, JavaScript
Backend	ASP.NET Core, C#
Database	Google Cloud Firestore
Authentication	Session-based with role management
Hosting	Google Cloud Platform
🚀 Quick Start
Prerequisites
.NET 6.0 SDK or later

Google Cloud Project with Firestore

Modern web browser

Installation
bash
# Clone the repository
git clone https://github.com/Mulongoni123/the-rock-waste-management.git

# Navigate to project directory
cd the-rock-waste-management

# Restore dependencies
dotnet restore

# Configure Firestore
# Set up your Google Cloud credentials
# Update appsettings.json with your project ID

# Run the application
dotnet run
📁 Project Structure
text
TheRockWasteManagement/
├── Controllers/
│   ├── CustomerController.cs
│   ├── AdminController.cs
│   ├── WorkerController.cs
│   └── AuthController.cs
├── Views/
│   ├── Customer/
│   │   ├── Dashboard.cshtml
│   │   ├── BookCleaning.cshtml
│   │   └── Profile.cshtml
│   ├── Admin/
│   └── Shared/
├── Models/
├── Services/
└── Program.cs
🎮 Usage
For Customers
Register/Login to your account

Book Services through the intuitive interface

Track Progress with real-time updates

Make Payments securely

Get Support when needed

For Administrators
Monitor System through comprehensive dashboard

Manage Users and permissions

Assign Workers to service requests

Track Performance with analytics

🔧 Key Code Snippets
Customer Booking
csharp
[HttpPost("BookCleaning")]
public async Task<IActionResult> BookCleaning(DateTime bookingDate, string address, string serviceType)
{
    // Smart booking with conflict detection
    var hasActiveBooking = await CheckActiveBooking(bookingDate);
    if (hasActiveBooking)
    {
        ModelState.AddModelError(string.Empty, "Active booking exists for this date");
        return View();
    }
    
    // Create booking logic
    var booking = new Dictionary<string, object>
    {
        { "CustomerId", uid },
        { "BookingDate", Timestamp.FromDateTime(bookingDate.ToUniversalTime()) },
        { "Status", "pending" }
    };
    
    await _firestoreDb.Collection("bookings").AddAsync(booking);
    return RedirectToAction("Dashboard");
}
Real-time Notifications
csharp
private async Task<List<Notification>> GetRecentNotifications(string customerId)
{
    var snapshot = await _firestoreDb.Collection("notifications")
        .WhereEqualTo("CustomerId", customerId)
        .OrderByDescending("CreatedAt")
        .Limit(5)
        .GetSnapshotAsync();
    
    return snapshot.Documents.Select(doc => new Notification
    {
        Title = doc.GetValue<string>("Title"),
        Message = doc.GetValue<string>("Message"),
        CreatedAt = doc.GetValue<Timestamp>("CreatedAt").ToDateTime()
    }).ToList();
}
📊 Database Schema
Main Collections
users: Customer, worker, and admin profiles

bookings: Service requests and scheduling

payments: Transaction records

support_tickets: Customer support system

notifications: System alerts and updates

assignments: Worker task allocations

🤝 Contributing
We welcome contributions! Please feel free to submit issues, fork the repository, and create pull requests.

Fork the Project

Create your Feature Branch (git checkout -b feature/AmazingFeature)

Commit your Changes (git commit -m 'Add some AmazingFeature')

Push to the Branch (git push origin feature/AmazingFeature)

Open a Pull Request

📝 License
This project is licensed under the MIT License - see the LICENSE.md file for details.

👨‍💻 Author
Your Name:Mulongoni Washu

🙏 Acknowledgments
ASP.NET Core team for the amazing framework

Google Cloud for Firestore database

Bootstrap team for the responsive UI components

All contributors and testers

📞 Contact
If you have any questions, feel free to reach out:

📧 Email:Mulongoniwashu3@gmail.com

💼 LinkedIn: Washu Mulongoni



<div align="center">
⭐ Don't forget to star this repository if you found it helpful!
Built with ❤️ using ASP.NET Core

</div>
