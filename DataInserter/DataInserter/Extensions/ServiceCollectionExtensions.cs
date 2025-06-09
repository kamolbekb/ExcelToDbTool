using DataInserter.Configuration;
using DataInserter.Models;
using DataInserter.Repositories.Implementations;
using DataInserter.Repositories.Interfaces;
using DataInserter.Services.Implementations;
using DataInserter.Services.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DataInserter.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDataInserterServices(this IServiceCollection services, IConfiguration configuration)
    {
        // Configuration
        services.Configure<ApplicationConfiguration>(configuration.GetSection("ApplicationSettings"));
        services.Configure<UserCommonFields>(configuration.GetSection("UserCommonFields"));

        // Services
        services.AddSingleton<IExcelReaderService, ExcelReaderService>();
        services.AddSingleton<IDuplicateHandlerService, DuplicateHandlerService>();
        services.AddScoped<IUserProcessingService, UserProcessingService>();

        // Repositories
        services.AddScoped<IIamRepository, IamRepository>();
        services.AddScoped<ISdgRepository, SdgRepository>();

        return services;
    }
}
