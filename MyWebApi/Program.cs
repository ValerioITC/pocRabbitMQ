var builder = WebApplication.CreateBuilder(args);

// Aggiungi i servizi necessari
builder.Services.AddHostedService<Worker>();
builder.Services.AddControllers(); // Aggiungi il supporto per i controller
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(); // Aggiungi Swagger
builder.Services.AddCors(); // Aggiungi il servizio CORS

// Aggiungi SignalR
builder.Services.AddSignalR();

// Aumenta il limite di dimensione del corpo della richiesta
builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.Limits.MaxRequestBodySize = 100 * 1024 * 1024; // 100 MB
});

var app = builder.Build();

// Configura la pipeline HTTP
if (app.Environment.IsDevelopment())
{
    app.UseSwagger(); // Abilita Swagger
    app.UseSwaggerUI(); // Abilita l'interfaccia utente di Swagger
}

// Configura CORS
app.UseCors(builder => builder
    .WithOrigins("http://localhost:5193") // Sostituisci con l'URL del tuo frontend
    .AllowAnyMethod()
    .AllowAnyHeader()
    .AllowCredentials()); // <-- Aggiungi questa linea per SignalR

app.UseHttpsRedirection(); // Reindirizza le richieste HTTP a HTTPS
app.UseRouting(); // Abilita il routing

// Abilita il middleware per i file statici
app.UseStaticFiles();

// Configura SignalR
app.MapHub<ConversionHub>("/conversionHub");

app.MapControllers(); // Mappa i controller

app.Run();