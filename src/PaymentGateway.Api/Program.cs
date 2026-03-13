using System.Text.Json.Serialization;
using PaymentGateway.Api.Application.Abstractions;
using PaymentGateway.Api.Application.Payments;
using PaymentGateway.Api.Infrastructure.Bank;
using PaymentGateway.Api.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    });
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.Configure<AcquiringBankOptions>(
    builder.Configuration.GetSection(AcquiringBankOptions.SectionName));

builder.Services.AddSingleton<IPaymentRepository, InMemoryPaymentRepository>();
builder.Services.AddScoped<PaymentService>();
builder.Services.AddHttpClient<IAcquiringBankClient, AcquiringBankClient>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

public partial class Program;
