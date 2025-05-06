using System;
using System.Threading.Tasks;
using Justloccit.Models;
using Justloccit.Services;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace Justloccit.Function
{
    public class TriggerDeleteTimeslotsByDate
    {
        private readonly ICosmosDbService _cosmosDbService;
        private readonly ILogger<TriggerDeleteTimeslotsByDate> _logger;

        public TriggerDeleteTimeslotsByDate(ICosmosDbService cosmosDbService, ILogger<TriggerDeleteTimeslotsByDate> logger)
        {
            _cosmosDbService = cosmosDbService;
            _logger = logger;
        }

        /// <summary>
        /// Daily timer-triggered function that deletes timeslots with dates before today.
        /// Runs at 00:05 UTC every day
        /// </summary>
        [Function("TriggerDeleteTimeslotsByDate")]
        public async Task Run([TimerTrigger("0 5 0 * * *")] TimerInfo timerInfo)
        {
            _logger.LogInformation("TriggerDeleteTimeslotsByDate function executed at: {ExecutionTime}", DateTime.UtcNow);

            try
            {
                // Get today's date in YYYY-MM-DD format
                string today = DateTime.UtcNow.Date.ToString("yyyy-MM-dd");
                _logger.LogInformation("Deleting timeslots with date before {Today}", today);

                // Get all documents with date before today
                var outdatedTimeslots = await _cosmosDbService.GetTimeslotsBeforeDateAsync(today);
                int count = 0;

                // Delete each document
                foreach (var timeslot in outdatedTimeslots)
                {
                    try
                    {
                        await _cosmosDbService.DeleteTimeslotAsync(timeslot.Id, timeslot.PartitionKey);
                        count++;
                        _logger.LogInformation("Deleted timeslot for date {Date}", timeslot.Date);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting timeslot for date {Date}", timeslot.Date);
                    }
                }

                _logger.LogInformation("Deleted {Count} outdated timeslots", count);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in TriggerDeleteTimeslotsByDate function");
            }
        }
    }
} 