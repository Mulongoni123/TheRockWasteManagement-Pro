using Google.Cloud.Firestore;
using Google.Cloud.Storage.V1;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllersWithViews();

// Enable session
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Firebase/Firestore configuration - FIXED FOR CLOUD RUN
try
{
    // For Cloud Run, use application default credentials
    FirestoreDb firestoreDb = FirestoreDb.Create("therockwastemanagement");
    builder.Services.AddSingleton(firestoreDb);

    // Optional: Initialize Storage client if needed
    var storageClient = StorageClient.Create();
    builder.Services.AddSingleton(storageClient);
}
catch (Exception ex)
{
    // Log error but don't crash - app might work without Firebase in dev
    Console.WriteLine($"Firebase initialization failed: {ex.Message}");
}

var app = builder.Build();

// Configure the HTTP request pipeline
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts(); // Add this for production
}

app.UseStaticFiles();
app.UseRouting();
app.UseSession(); // Enable session before endpoints
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Use PORT environment variable for Cloud Run - REMOVED DUPLICATE app.Run()
var port = Environment.GetEnvironmentVariable("PORT") ?? "8080";
app.Run($"http://0.0.0.0:{port}");