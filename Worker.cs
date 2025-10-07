using System.Diagnostics.Eventing.Reader;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.Net;
using System.Net.Mail;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace LdapMonitoring;

[SupportedOSPlatform("windows")]
public class Worker(IConfiguration config, ILogger<Worker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started at: {time}", DateTimeOffset.Now);
        
        const string query = "*[System/EventID=4720]";
        var eventLogQuery = new EventLogQuery("Security", PathType.LogName, query);
        var watcher = new EventLogWatcher(eventLogQuery);

        watcher.EventRecordWritten += (_, e) =>
        {
            if (e.EventRecord == null) return;

            try
            {
                string? targetUser = e.EventRecord.Properties[0].Value.ToString();
                string? creatorUser = e.EventRecord.Properties[4].Value.ToString();

                using var context = new PrincipalContext(ContextType.Domain);
                using var user = UserPrincipal.FindByIdentity(context, targetUser);
                using var creator = UserPrincipal.FindByIdentity(context, creatorUser);
                
                if (user == null) return;
                
                var fullName = $"{user.Surname} {user.GivenName} {user.MiddleName}";
                var directoryEntry = (DirectoryEntry)user.GetUnderlyingObject();
                var createdAt = directoryEntry.Properties["whenCreated"].Value + " (UTC)"
                                ?? throw new InvalidOperationException();
                
                SendEmail(user.Sid, user.UserPrincipalName, fullName, createdAt, creator.UserPrincipalName);
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "[ERROR] Error processing event");
            }
        };

        watcher.Enabled = true;
        
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }

        watcher.Enabled = false;
        watcher.Dispose();
        
        logger.LogInformation("Worker stopped at: {time}", DateTimeOffset.Now);
    }

    private void SendEmail(SecurityIdentifier sid, string upn, string fullName, string createdAt, string createdBy)
    {
        string sender = config["SMTP:Sender"]!;
        var recipients = config.GetSection("SMTP:Recipient").Get<List<string>>()!;
        string smtpHost = config["SMTP:Host"]!;
        int smtpPort = int.Parse(config["SMTP:Port"]!);
        bool enableSsl = config.GetValue<bool>("SMTP:EnableSsl");
        string smtpLogin = config["SMTP:Login"]!;
        string smtpPassword = config["SMTP:Password"]!;

        string letterTemplate = File.ReadAllText("template.html");
        
        var message = new MailMessage();
        
        message.From = new MailAddress(sender);

        foreach (var recipient in recipients)
        {
            message.To.Add(recipient);
        }
        
        message.Subject = "Active Directory notification: new user has been added";
        message.Body = letterTemplate
            .Replace("{sid}", sid.ToString())
            .Replace("{upn}", upn)
            .Replace("{fullName}", fullName)
            .Replace("{createdAt}", createdAt)
            .Replace("{createdBy}", createdBy);
        message.IsBodyHtml = true;

        using var client = new SmtpClient(smtpHost, smtpPort);
        client.Credentials = new NetworkCredential(smtpLogin, smtpPassword);
        client.EnableSsl = enableSsl;
        client.Send(message);
    }
}