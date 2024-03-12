using ConsumerQueue.BackgroundServices;
using ConsumerQueue.DTO;
using ProcessQueue.DTO;

var awsSettings = new AwsSettingsDTO();
var builder = WebApplication.CreateBuilder(args);
builder.Configuration.Bind("AwsConfiguration", awsSettings);
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHostedService<ConsumerBackgroundService>();
builder.Services.AddSingleton<MessageProcessedDTO>();
builder.Services.AddSingleton(awsSettings);

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();

await app.RunAsync();
