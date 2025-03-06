using Microsoft.AspNetCore.SignalR;

public class ConversionHub : Hub
{
    public async Task NotifyConversionSuccess(string fileName)
    {
        await Clients.All.SendAsync("ConversionSuccess", fileName);
    }
}