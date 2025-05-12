// Default URL for triggering event grid function in the local environment.
// http://localhost:7071/runtime/webhooks/EventGrid?functionName={functionname}

using System;
using System.Text;
using System.Threading.Tasks;
using Azure.Messaging;
using Azure.Communication.Email;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Text.Json;

namespace Justloccit.Function
{
    public class BookingConfirmedEmail
    {
        private readonly ILogger<BookingConfirmedEmail> _logger;
        private readonly EmailClient _emailClient;
        private readonly string _adminEmails;
        private readonly string _senderAddress;

        public BookingConfirmedEmail(ILogger<BookingConfirmedEmail> logger, EmailClient emailClient)
        {
            _logger = logger;
            _emailClient = emailClient;
            _adminEmails = "kevinsamuel@justloccit.dk,idabro@justloccit.dk";
            _senderAddress = Environment.GetEnvironmentVariable("SenderEmailAddress") ?? 
                            "donotreply@justloccit.dk";
        }

        [Function(nameof(BookingConfirmedEmail))]
        public async Task Run([EventGridTrigger] CloudEvent cloudEvent)
        {
            _logger.LogInformation("Event type: {type}, Event subject: {subject}", cloudEvent.Type, cloudEvent.Subject);
            
            try
            {
                if (cloudEvent.Type != "Justloccit.Booking.Confirmed")
                {
                    _logger.LogInformation("Ignoring event type: {type}", cloudEvent.Type);
                    return;
                }

                // Deserialize the event data
                var bookingData = JsonDocument.Parse(cloudEvent.Data.ToString())
                    .RootElement.Deserialize<BookingEventData>(new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });

                if (bookingData == null)
                {
                    _logger.LogError("Failed to deserialize booking data");
                    return;
                }

                // Send email to customer if email is available
                if (!string.IsNullOrEmpty(bookingData.CustomerEmail))
                {
                    await SendCustomerEmailAsync(bookingData);
                }
                else
                {
                    _logger.LogWarning($"Customer email is missing for booking {bookingData.Id}");
                }

                // Send email to admins
                await SendAdminEmailAsync(bookingData);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing booking confirmed event");
            }
        }

        private async Task SendCustomerEmailAsync(BookingEventData bookingData)
        {
            try
            {
                var subject = $"Booking Confirmation - {bookingData.Date:d}";
                var htmlContent = CreateCustomerEmailHtml(bookingData);

                var emailSendOperation = await _emailClient.SendAsync(
                    Azure.WaitUntil.Started,
                    senderAddress: _senderAddress,
                    recipientAddress: bookingData.CustomerEmail,
                    subject: subject,
                    htmlContent: htmlContent);
                
                _logger.LogInformation($"Email sent to customer {bookingData.CustomerEmail} for booking {bookingData.Id}. Operation ID: {emailSendOperation.Id}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending email to customer {bookingData.CustomerEmail} for booking {bookingData.Id}");
            }
        }

        private async Task SendAdminEmailAsync(BookingEventData bookingData)
        {
            try
            {
                var subject = $"Booking Confirmed - {bookingData.Date:d}";
                var htmlContent = CreateAdminEmailHtml(bookingData);

                foreach (var adminEmail in _adminEmails.Split(','))
                {
                    var emailSendOperation = await _emailClient.SendAsync(
                        Azure.WaitUntil.Started,
                        senderAddress: _senderAddress,
                        recipientAddress: adminEmail.Trim(),
                        subject: subject,
                        htmlContent: htmlContent);
                    
                    _logger.LogInformation($"Email sent to admin {adminEmail} for booking {bookingData.Id}. Operation ID: {emailSendOperation.Id}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error sending admin email for booking {bookingData.Id}");
            }
        }

        private string CreateCustomerEmailHtml(BookingEventData bookingData)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
            sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }");
            sb.AppendLine("h1 { color: #2c3e50; }");
            sb.AppendLine(".booking-details { background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            sb.AppendLine(".booking-status { display: inline-block; padding: 5px 10px; border-radius: 3px; background-color: #28a745; color: #fff; }");
            sb.AppendLine(".footer { margin-top: 30px; padding-top: 10px; border-top: 1px solid #ddd; font-size: 12px; color: #777; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class='container'>");
            sb.AppendLine($"<h1>Booking Confirmation</h1>");
            sb.AppendLine($"<p>Dear {bookingData.CustomerName},</p>");
            sb.AppendLine("<p>Great news! Your booking with Justloccit has been confirmed. Your booking details are as follows:</p>");
            sb.AppendLine("<div class='booking-details'>");
            sb.AppendLine($"<p><strong>Booking ID:</strong> {bookingData.Id}</p>");
            sb.AppendLine($"<p><strong>Date:</strong> {bookingData.Date:dddd, MMMM d, yyyy}</p>");
            sb.AppendLine($"<p><strong>Time:</strong> {FormatTimeSpan(bookingData.StartTime)} - {FormatTimeSpan(bookingData.EndTime)}</p>");
            sb.AppendLine($"<p><strong>Status:</strong> <span class='booking-status'>Confirmed</span></p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<p>We look forward to providing you with excellent service.</p>");
            sb.AppendLine("<p>If you need to make any changes to your booking, please contact us as soon as possible.</p>");
            sb.AppendLine("<p>Best regards,<br>The Justloccit Team</p>");
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>This is an automated message, please do not reply directly to this email.</p>");
            sb.AppendLine("<p>&copy; " + DateTime.Now.Year + " Justloccit. All rights reserved.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        private string CreateAdminEmailHtml(BookingEventData bookingData)
        {
            var sb = new StringBuilder();
            sb.AppendLine("<!DOCTYPE html>");
            sb.AppendLine("<html>");
            sb.AppendLine("<head>");
            sb.AppendLine("<style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; line-height: 1.6; color: #333; }");
            sb.AppendLine(".container { max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px; }");
            sb.AppendLine("h1 { color: #2c3e50; }");
            sb.AppendLine(".booking-details { background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            sb.AppendLine(".customer-details { background-color: #e9f7ef; padding: 15px; border-radius: 5px; margin: 20px 0; }");
            sb.AppendLine(".booking-status { display: inline-block; padding: 5px 10px; border-radius: 3px; background-color: #28a745; color: #fff; }");
            sb.AppendLine(".footer { margin-top: 30px; padding-top: 10px; border-top: 1px solid #ddd; font-size: 12px; color: #777; }");
            sb.AppendLine("</style>");
            sb.AppendLine("</head>");
            sb.AppendLine("<body>");
            sb.AppendLine("<div class='container'>");
            sb.AppendLine($"<h1>Booking Confirmed</h1>");
            sb.AppendLine("<p>A booking has been confirmed with the following details:</p>");
            sb.AppendLine("<div class='booking-details'>");
            sb.AppendLine($"<p><strong>Booking ID:</strong> {bookingData.Id}</p>");
            sb.AppendLine($"<p><strong>Date:</strong> {bookingData.Date:dddd, MMMM d, yyyy}</p>");
            sb.AppendLine($"<p><strong>Time:</strong> {FormatTimeSpan(bookingData.StartTime)} - {FormatTimeSpan(bookingData.EndTime)}</p>");
            sb.AppendLine($"<p><strong>Status:</strong> <span class='booking-status'>Confirmed</span></p>");
            sb.AppendLine($"<p><strong>Service ID:</strong> {bookingData.SubServiceId}</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='customer-details'>");
            sb.AppendLine("<h2>Customer Information</h2>");
            sb.AppendLine($"<p><strong>Customer ID:</strong> {bookingData.CustomerId}</p>");
            sb.AppendLine($"<p><strong>Name:</strong> {bookingData.CustomerName}</p>");
            sb.AppendLine($"<p><strong>Email:</strong> {bookingData.CustomerEmail}</p>");
            sb.AppendLine($"<p><strong>Phone:</strong> {bookingData.CustomerPhone}</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("<div class='footer'>");
            sb.AppendLine("<p>This is an automated message from the Justloccit booking system.</p>");
            sb.AppendLine("<p>&copy; " + DateTime.Now.Year + " Justloccit. All rights reserved.</p>");
            sb.AppendLine("</div>");
            sb.AppendLine("</div>");
            sb.AppendLine("</body>");
            sb.AppendLine("</html>");
            
            return sb.ToString();
        }

        private string FormatTimeSpan(TimeSpan time)
        {
            return time.ToString(@"hh\:mm");
        }
    }

    public class BookingEventData
    {
        public string Id { get; set; } = string.Empty;
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string CustomerEmail { get; set; } = string.Empty;
        public string CustomerPhone { get; set; } = string.Empty;
        public string SubServiceId { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public TimeSpan StartTime { get; set; }
        public TimeSpan EndTime { get; set; }
        public int BookingStatus { get; set; }
        public string EventType { get; set; } = string.Empty;
    }
}
