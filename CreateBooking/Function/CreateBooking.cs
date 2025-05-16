using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Justloccit.Function.Data;
using Justloccit.Function.Models;

namespace Justloccit.Function
{
    public class CreateBooking
    {
        private readonly ILogger<CreateBooking> _logger;
        private readonly CosmosDbService _cosmos;

        public CreateBooking(ILogger<CreateBooking> logger, CosmosDbService cosmos) =>
            (_logger, _cosmos) = (logger, cosmos);

        [Function(nameof(CreateBooking))]
        public async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "createbooking")] HttpRequest req,
            CancellationToken ct)
        {
            // 1. Read & deserialize ---------------------------------------------------------
            var body = await new StreamReader(req.Body).ReadToEndAsync(ct);
            if (string.IsNullOrWhiteSpace(body))
                return BadRequest("Request body is empty");

            if (!TryDeserialize(body, out var dto))
                return BadRequest("Invalid request format");

            // 2. Validate primitive rules with guard clauses -------------------------------
            if (string.IsNullOrWhiteSpace(dto.SubServiceId))
                return BadRequest("SubServiceId is required");

            // 3. Resolve domain entities ----------------------------------------------------
            var customer = await GetOrCreateCustomerAsync(dto, ct);
            if (customer == null)
                return BadRequest("Either CustomerId must exist or CustomerInfo must be supplied");

            var sub = await GetSubServiceAsync(dto.SubServiceId, ct);
            if (sub == null)
                return BadRequest($"SubService with ID {dto.SubServiceId} not found");

            // 4. Persist booking ------------------------------------------------------------
            var booking = new BookingModel
            {
                Id             = Guid.NewGuid().ToString(),
                CustomerId     = customer.Id,
                CustomerName   = customer.Name,
                CustomerEmail  = customer.Email,
                CustomerPhone  = customer.Phone,
                SubServiceId   = dto.SubServiceId,
                SubServiceName = sub.name,
                ServiceId      = sub.serviceId,
                ServiceName    = sub.serviceName,
                Date           = dto.Date,
                StartTime      = dto.StartTime,
                EndTime        = dto.EndTime,
                Notes          = dto.Notes ?? string.Empty,
                BookingStatus  = BookingStatus.Pending,
                CreatedAt      = DateTime.UtcNow,
                UpdatedAt      = DateTime.UtcNow
            };

            await _cosmos.CreateItemAsync("Bookings", booking, booking.CustomerId);

            _logger.LogInformation("Booking {BookingId} created", booking.Id);

            // 5. Respond --------------------------------------------------------------------
            return new OkObjectResult(new CreateBookingResponse
            {
                Success = true,
                BookingId = booking.Id,
                CustomerId = booking.CustomerId,
                Date = booking.Date,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime
            });
        }

        // -------------------------------------------------------------------------------
        private static bool TryDeserialize(string json, out CreateBookingRequest dto)
        {
            dto = JsonConvert.DeserializeObject<CreateBookingRequest>(json);
            return dto != null;
        }

        private async Task<CustomerModel?> GetOrCreateCustomerAsync(
            CreateBookingRequest dto,
            CancellationToken ct)
        {
            // Existing customer? ----------------------------------------------------------
            if (!string.IsNullOrWhiteSpace(dto.CustomerId))
            {
                var existing = await _cosmos.GetItemAsync<CustomerModel>("Customers", dto.CustomerId);
                if (existing != null) return existing;
                if (dto.CustomerInfo == null) return null; // invalid combination
            }

            // Create new -----------------------------------------------------------------
            var id   = dto.CustomerId ?? Guid.NewGuid().ToString();
            var info = dto.CustomerInfo!; // guaranteed non-null here

            var customer = new CustomerModel
            {
                Id        = id,
                Name      = info.Name,
                Email     = info.Email,
                Phone     = info.Phone,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            await _cosmos.CreateItemAsync("Customers", customer, customer.Email);
            dto.CustomerId = id; // propagate back
            return customer;
        }

        private Task<dynamic?> GetSubServiceAsync(string id, CancellationToken ct) =>
            _cosmos.GetItemAsync<dynamic>("SubServices", id);

        private static BadRequestObjectResult BadRequest(string message) =>
            new(new CreateBookingResponse { Success = false, Message = message });
    }
}   