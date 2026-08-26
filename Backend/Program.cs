using Serilog;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Backend.Data;
using Backend.Models;
using REACT_ASP.DataAccesslayer;
using REACT_ASP.Model;
using Microsoft.AspNetCore.ResponseCompression;
using System.IO.Compression;
using Backend.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Настройка Serilog из конфигурации
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .Enrich.WithProcessId()
    .CreateLogger();

builder.Host.UseSerilog();

var jwtSecretKey = builder.Configuration["Jwt:SecretKey"] ?? "your-super-secret-key-with-at-least-32-characters-long";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "GlanceVexAPI";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "GlanceVexClient";

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
        options.JsonSerializerOptions.WriteIndented = false;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.MaxDepth = 32;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddScoped<IAuthDl, AuthDl>();
builder.Services.AddScoped<JwtService>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDb>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.CommandTimeout(30);
        npgsqlOptions.MaxBatchSize(100);
        npgsqlOptions.EnableRetryOnFailure(3);
    });
    
    options.EnableSensitiveDataLogging(false);
    options.EnableDetailedErrors(false);
    options.EnableServiceProviderCaching(true);
    options.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
    options.EnableThreadSafetyChecks(false);
});

builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
    options.Providers.Add<BrotliCompressionProvider>();
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Fastest;
});

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidIssuer = jwtIssuer,
        ValidateAudience = true,
        ValidAudience = jwtAudience,
        ValidateLifetime = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecretKey)),
        ValidateIssuerSigningKey = true,
        ClockSkew = TimeSpan.Zero
    };
    
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var token = context.Request.Headers["Authorization"].FirstOrDefault();
            if (!string.IsNullOrEmpty(token) && token.StartsWith("Bearer "))
            {
                context.Token = token.Substring("Bearer ".Length).Trim();
            }
            return Task.CompletedTask;
        },
        OnAuthenticationFailed = context =>
        {
            Log.Warning("JWT Authentication failed: {Error}", context.Exception.Message);
            return Task.CompletedTask;
        }
    };
});

builder.Services.AddAuthorization();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend",
        policy =>
        {
            policy.WithOrigins("http://localhost:3000", "http://127.0.0.1:5500")
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetPreflightMaxAge(TimeSpan.FromDays(1));
        });
});

builder.Services.AddScoped<IEncryptionService, EncryptionService>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;

    try
    {
        var context = services.GetRequiredService<ApplicationDb>();
        await context.Database.MigrateAsync();

        var productsExist = await context.Products.AnyAsync();
        var categoriesExist = await context.Categories.AnyAsync();
        
        if (!productsExist || !categoriesExist)
        {
            Log.Information("Данные отсутствуют, выполняем инициализацию...");
            await ForceInitializeDatabaseAsync(context);
            Log.Information("Инициализация завершена");
        }
        else
        {
            var imagesExist = await context.ProductImages.AnyAsync();
            if (!imagesExist)
            {
                Log.Information("Изображения отсутствуют, добавляем...");
                await ForceAddAllProductImages(context);
                Log.Information("Изображения добавлены");
            }
            else
            {
                Log.Information("База данных уже содержит данные: {ProductsCount} товаров, {ImagesCount} изображений", 
                    await context.Products.CountAsync(), 
                    await context.ProductImages.CountAsync());
            }
        }
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при инициализации базы данных");
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Error during database initialization");
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseResponseCompression();

app.UseStaticFiles();

app.UseAuthentication(); 
app.UseAuthorization();  

app.MapGet("/api/test", () => Results.Ok(new { message = "API работает!", time = DateTime.Now }));

// КОМПЬЮТЕРЫ
app.MapGet("/api/computers", async (ApplicationDb context, HttpContext httpContext) =>
{
    var category = await context.Categories
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Name == "Компьютеры");
    
    if (category == null)
        return Results.Ok(new List<object>());
    
    var computers = await context.Products
        .AsNoTracking()
        .Where(p => p.CategoryId == category.Id && p.IsActive)
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            BrandName = p.Brand != null ? p.Brand.Name : "Не указан",
            p.IsActive,
            ImageUrl = p.Images != null && p.Images.Any() 
                ? p.Images.First(i => i.IsMain).ImageUrl 
                : (p.Images != null && p.Images.Any() ? p.Images.First().ImageUrl : null)
        })
        .Take(5)
        .ToListAsync();
    
    httpContext.Response.Headers["Cache-Control"] = "public, max-age=300";
    return Results.Ok(computers);
});

// НОУТБУКИ
app.MapGet("/api/laptops", async (ApplicationDb context, HttpContext httpContext) =>
{
    var category = await context.Categories
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Name == "Ноутбуки");
    
    if (category == null)
        return Results.Ok(new List<object>());
    
    var laptops = await context.Products
        .AsNoTracking()
        .Where(p => p.CategoryId == category.Id && p.IsActive)
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            BrandName = p.Brand != null ? p.Brand.Name : "Не указан",
            p.IsActive,
            ImageUrl = p.Images != null && p.Images.Any() 
                ? p.Images.First(i => i.IsMain).ImageUrl 
                : (p.Images != null && p.Images.Any() ? p.Images.First().ImageUrl : null)
        })
        .Take(5)
        .ToListAsync();
    
    httpContext.Response.Headers["Cache-Control"] = "public, max-age=300";
    return Results.Ok(laptops);
});

// ТЕЛЕВИЗОРЫ
app.MapGet("/api/tvs", async (ApplicationDb context, HttpContext httpContext) =>
{
    var category = await context.Categories
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Name == "Телевизоры");
    
    if (category == null)
        return Results.Ok(new List<object>());
    
    var tvs = await context.Products
        .AsNoTracking()
        .Where(p => p.CategoryId == category.Id && p.IsActive)
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            BrandName = p.Brand != null ? p.Brand.Name : "Не указан",
            p.IsActive,
            ImageUrl = p.Images != null && p.Images.Any() 
                ? p.Images.First(i => i.IsMain).ImageUrl 
                : (p.Images != null && p.Images.Any() ? p.Images.First().ImageUrl : null)
        })
        .Take(5)
        .ToListAsync();
    
    httpContext.Response.Headers["Cache-Control"] = "public, max-age=300";
    return Results.Ok(tvs);
});

// СМАРТФОНЫ
app.MapGet("/api/smartphones", async (ApplicationDb context, HttpContext httpContext) =>
{
    var category = await context.Categories
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Name == "Смартфоны");
    
    if (category == null)
        return Results.Ok(new List<object>());
    
    var smartphones = await context.Products
        .AsNoTracking()
        .Where(p => p.CategoryId == category.Id && p.IsActive)
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            BrandName = p.Brand != null ? p.Brand.Name : "Не указан",
            p.IsActive,
            ImageUrl = p.Images != null && p.Images.Any() 
                ? p.Images.First(i => i.IsMain).ImageUrl 
                : (p.Images != null && p.Images.Any() ? p.Images.First().ImageUrl : null)
        })
        .Take(5)
        .ToListAsync();
    
    httpContext.Response.Headers["Cache-Control"] = "public, max-age=300";
    return Results.Ok(smartphones);
});

// КОЛОНКИ
app.MapGet("/api/speakers", async (ApplicationDb context, HttpContext httpContext) =>
{
    var category = await context.Categories
        .AsNoTracking()
        .FirstOrDefaultAsync(c => c.Name == "Колонки");
    
    if (category == null)
        return Results.Ok(new List<object>());
    
    var speakers = await context.Products
        .AsNoTracking()
        .Where(p => p.CategoryId == category.Id && p.IsActive)
        .OrderBy(p => p.Id)
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            BrandName = p.Brand != null ? p.Brand.Name : "Не указан",
            p.IsActive,
            ImageUrl = p.Images != null && p.Images.Any() 
                ? p.Images.First(i => i.IsMain).ImageUrl 
                : (p.Images != null && p.Images.Any() ? p.Images.First().ImageUrl : null)
        })
        .Take(5)
        .ToListAsync();
    
    httpContext.Response.Headers["Cache-Control"] = "public, max-age=300";
    return Results.Ok(speakers);
});

// Поиск товаров
app.MapGet("/api/products/search", async (ApplicationDb context, 
    string q, 
    int? categoryId = null, 
    decimal? minPrice = null, 
    decimal? maxPrice = null) =>
{
    var query = context.Products
        .Where(p => p.IsActive)
        .Include(p => p.Brand)
        .Include(p => p.Category)
        .Include(p => p.Images)
        .AsQueryable();

    if (!string.IsNullOrEmpty(q))
    {
        query = query.Where(p => 
            p.Name.ToLower().Contains(q.ToLower()) || 
            (p.Description != null && p.Description.ToLower().Contains(q.ToLower())));
    }

    if (categoryId.HasValue)
    {
        query = query.Where(p => p.CategoryId == categoryId.Value);
    }

    if (minPrice.HasValue)
    {
        query = query.Where(p => p.Price >= minPrice.Value);
    }
    if (maxPrice.HasValue)
    {
        query = query.Where(p => p.Price <= maxPrice.Value);
    }

    var results = await query
        .Select(p => new
        {
            p.Id,
            p.Name,
            p.Price,
            p.Description,
            p.IsActive,
            BrandName = p.Brand != null ? p.Brand.Name : null,
            CategoryName = p.Category != null ? p.Category.Name : null,
            MainImage = p.Images != null && p.Images.Any() 
                ? p.Images.First(i => i.IsMain).ImageUrl 
                : (p.Images != null && p.Images.Any() ? p.Images.First().ImageUrl : null)
        })
        .ToListAsync();

    return Results.Ok(results);
});

app.MapGet("/api/categories/with-products", async (ApplicationDb context, HttpContext httpContext) =>
{
    var categories = await context.Categories
        .AsNoTracking()
        .Select(c => new
        {
            c.Id,
            c.Name,
            c.Description,
            c.ImageUrl,
            ProductCount = context.Products.Count(p => p.CategoryId == c.Id && p.IsActive)
        })
        .OrderBy(c => c.Id)
        .ToListAsync();
    
    httpContext.Response.Headers["Cache-Control"] = "public, max-age=60";
    return Results.Ok(categories);
});

app.MapPost("/api/force-add-all-products", async (ApplicationDb context) =>
{
    try
    {
        Log.Information("Принудительное добавление всех товаров");
        
        await ForceCreateCategories(context);
        await ForceCreateBrandsAndTypes(context);
        await ForceCreateAttributes(context);
        await ForceAddComputers(context);
        await ForceAddLaptops(context);
        await ForceAddTvs(context);
        await ForceAddSmartphones(context);
        await ForceAddSpeakers(context);
        await ForceAddProductAttributeValues(context);
        await ForceAddAllProductImages(context);

        var result = new List<object>();
        var categories = await context.Categories.OrderBy(c => c.Id).ToListAsync();
        
        foreach (var cat in categories)
        {
            var count = await context.Products.CountAsync(p => p.CategoryId == cat.Id);
            var attrCount = await context.ProductAttributeValues.CountAsync(v => v.Product != null && v.Product.CategoryId == cat.Id);
            var imgCount = await context.ProductImages.CountAsync(i => i.Product != null && i.Product.CategoryId == cat.Id);
            result.Add(new { cat.Name, Products = count, Attributes = attrCount, Images = imgCount });
            Log.Information("  {Category}: {Products} товаров, {Attributes} характеристик, {Images} изображений", 
                cat.Name, count, attrCount, imgCount);
        }
        
        Log.Information("Все товары, характеристики и изображения успешно добавлены");
        
        return Results.Ok(new 
        { 
            success = true, 
            message = "Все товары, характеристики и изображения успешно добавлены",
            categories = result
        });
    }
    catch (Exception ex)
    {
        Log.Error(ex, "Ошибка при принудительном добавлении товаров");
        return Results.Problem($"Ошибка: {ex.Message}");
    }
});

app.MapControllers();

try
{
    Log.Information("Приложение запущено на http://localhost:5214");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Приложение неожиданно завершилось");
    throw;
}
finally
{
    Log.CloseAndFlush();
}

static async Task ForceInitializeDatabaseAsync(ApplicationDb context)
{
    Log.Information("ИНИЦИАЛИЗАЦИЯ БАЗЫ ДАННЫХ");

    await ClearAllProducts(context);
    await ForceCreateCategories(context);
    await ForceCreateBrandsAndTypes(context);
    await ForceCreateAttributes(context);
    await ForceAddComputers(context);
    await ForceAddLaptops(context);
    await ForceAddTvs(context);
    await ForceAddSmartphones(context);
    await ForceAddSpeakers(context);
    await ForceAddProductAttributeValues(context);
    await ForceAddAllProductImages(context);
    
    Log.Information("ИНИЦИАЛИЗАЦИЯ ЗАВЕРШЕНА");
}

static async Task ClearAllProducts(ApplicationDb context)
{
    Log.Information("ОЧИСТКА ВСЕХ ТОВАРОВ И СВЯЗАННЫХ ДАННЫХ");

    var values = await context.ProductAttributeValues.ToListAsync();
    context.ProductAttributeValues.RemoveRange(values);
    Log.Information("Удалено {Count} характеристик", values.Count);

    var images = await context.ProductImages.ToListAsync();
    context.ProductImages.RemoveRange(images);
    Log.Information("Удалено {Count} изображений", images.Count);

    var reviews = await context.Reviews.ToListAsync();
    context.Reviews.RemoveRange(reviews);
    Log.Information("Удалено {Count} отзывов", reviews.Count);

    var baskets = await context.Baskets.ToListAsync();
    context.Baskets.RemoveRange(baskets);
    Log.Information("Удалено {Count} элементов корзины", baskets.Count);

    var orderItems = await context.OrderItems.ToListAsync();
    context.OrderItems.RemoveRange(orderItems);
    Log.Information("Удалено {Count} элементов заказов", orderItems.Count);

    var purchases = await context.Purchases.ToListAsync();
    context.Purchases.RemoveRange(purchases);
    Log.Information("Удалено {Count} заказов", purchases.Count);

    var products = await context.Products.ToListAsync();
    context.Products.RemoveRange(products);
    Log.Information("Удалено {Count} товаров", products.Count);

    var attributes = await context.ProductAttributes.ToListAsync();
    context.ProductAttributes.RemoveRange(attributes);
    Log.Information("Удалено {Count} атрибутов", attributes.Count);

    var categories = await context.Categories.ToListAsync();
    context.Categories.RemoveRange(categories);
    Log.Information("Удалено {Count} категорий", categories.Count);

    var brands = await context.Brands.ToListAsync();
    context.Brands.RemoveRange(brands);
    Log.Information("Удалено {Count} брендов", brands.Count);
    
    await context.SaveChangesAsync();
    Log.Information("ОЧИСТКА ЗАВЕРШЕНА");
}

static async Task ForceCreateAttributes(ApplicationDb context)
{
    Log.Information("СОЗДАНИЕ АТРИБУТОВ");

    var oldValues = await context.ProductAttributeValues.ToListAsync();
    context.ProductAttributeValues.RemoveRange(oldValues);
    await context.SaveChangesAsync();
    Log.Information("Удалено {Count} значений атрибутов", oldValues.Count);

    var oldAttributes = await context.ProductAttributes.ToListAsync();
    context.ProductAttributes.RemoveRange(oldAttributes);
    await context.SaveChangesAsync();
    Log.Information("Удалено {Count} старых атрибутов", oldAttributes.Count);

    var attributes = new[]
    {
        new ProductAttribute { Name = "Процессор", AttributeGroup = "Основные", Unit = "" },
        new ProductAttribute { Name = "Оперативная память", AttributeGroup = "Основные", Unit = "ГБ" },
        new ProductAttribute { Name = "Накопитель", AttributeGroup = "Основные", Unit = "ГБ" },
        new ProductAttribute { Name = "Видеокарта", AttributeGroup = "Основные", Unit = "" },
        new ProductAttribute { Name = "Диагональ экрана", AttributeGroup = "Экран", Unit = "дюймы" },
        new ProductAttribute { Name = "Разрешение", AttributeGroup = "Экран", Unit = "" },
        new ProductAttribute { Name = "Частота обновления", AttributeGroup = "Экран", Unit = "Гц" },
        new ProductAttribute { Name = "Цвет", AttributeGroup = "Дизайн", Unit = "" },
        new ProductAttribute { Name = "Операционная система", AttributeGroup = "Софт", Unit = "" },
        new ProductAttribute { Name = "Smart TV", AttributeGroup = "Технологии", Unit = "" },
        new ProductAttribute { Name = "HDR", AttributeGroup = "Технологии", Unit = "" },
        new ProductAttribute { Name = "Bluetooth", AttributeGroup = "Технологии", Unit = "" },
        new ProductAttribute { Name = "Wi-Fi", AttributeGroup = "Технологии", Unit = "" },
        new ProductAttribute { Name = "Вес", AttributeGroup = "Физические", Unit = "кг" },
        new ProductAttribute { Name = "Материал", AttributeGroup = "Дизайн", Unit = "" },
        new ProductAttribute { Name = "Технология подсветки", AttributeGroup = "Экран", Unit = "" }
    };
    
    foreach (var attr in attributes)
    {
        var existing = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == attr.Name);
        if (existing == null)
        {
            context.ProductAttributes.Add(attr);
            Log.Information("  Создан атрибут: {Name}", attr.Name);
        }
        else
        {
            Log.Information("  Атрибут уже существует: {Name}", attr.Name);
        }
    }
    
    await context.SaveChangesAsync();
    Log.Information("Всего атрибутов: {Count}", await context.ProductAttributes.CountAsync());
}

static async Task ForceCreateCategories(ApplicationDb context)
{
    Log.Information("СОЗДАНИЕ КАТЕГОРИЙ");
    
    var categories = new[]
    {
        new { Name = "Телевизоры", Desc = "Телевизоры в нашем магазине" },
        new { Name = "Ноутбуки", Desc = "Ноутбуки в нашем магазине" },
        new { Name = "Компьютеры", Desc = "Компьютеры в нашем магазине" },
        new { Name = "Смартфоны", Desc = "Смартфоны в нашем магазине" },
        new { Name = "Колонки", Desc = "Колонки в нашем магазине" }
    };
    
    foreach (var cat in categories)
    {
        var existing = await context.Categories.FirstOrDefaultAsync(c => c.Name == cat.Name);
        if (existing == null)
        {
            context.Categories.Add(new Category { Name = cat.Name, Description = cat.Desc });
            Log.Information("  Создана категория: {Name}", cat.Name);
        }
        else
        {
            existing.Description = cat.Desc;
            Log.Information("  Категория уже существует: {Name}", cat.Name);
        }
    }
    
    await context.SaveChangesAsync();
}

static async Task ForceCreateBrandsAndTypes(ApplicationDb context)
{
    Log.Information("СОЗДАНИЕ БРЕНДОВ И ТИПОВ");
    
    var brandNames = new[] { "Samsung", "LG", "Sony", "Xiaomi", "Apple", "Lenovo", "ASUS", "HP", "Dell", "MSI", "JBL", "Sennheiser", "Google" };
    
    foreach (var name in brandNames)
    {
        if (!await context.Brands.AnyAsync(b => b.Name == name))
        {
            context.Brands.Add(new Brand { Name = name });
            Log.Information("  Создан бренд: {Name}", name);
        }
    }
    await context.SaveChangesAsync();
    
    var typeNames = new[] { "Телевизор", "Ноутбук", "Компьютер", "Смартфон", "Колонка" };
    
    foreach (var name in typeNames)
    {
        if (!await context.Types.AnyAsync(t => t.Name == name))
        {
            context.Types.Add(new REACT_ASP.Model.Type { Name = name });
            Log.Information("  Создан тип: {Name}", name);
        }
    }
    await context.SaveChangesAsync();
}

static async Task ForceAddProductAttributeValues(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ ХАРАКТЕРИСТИК ДЛЯ ТОВАРОВ");

    var products = await context.Products
        .Include(p => p.Category)
        .ToListAsync();

    var processorAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Процессор");
    var ramAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Оперативная память");
    var storageAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Накопитель");
    var gpuAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Видеокарта");
    var diagonalAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Диагональ экрана");
    var resolutionAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Разрешение");
    var refreshRateAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Частота обновления");
    var colorAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Цвет");
    var osAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Операционная система");
    var smartTvAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Smart TV");
    var hdrAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "HDR");
    var bluetoothAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Bluetooth");
    var wifiAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Wi-Fi");
    var weightAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Вес");
    var backlightAttr = await context.ProductAttributes.FirstOrDefaultAsync(a => a.Name == "Технология подсветки");

    Log.Information("Найдены атрибуты:");
    Log.Information("  Процессор ID: {Id}", processorAttr?.Id);
    Log.Information("  Оперативная память ID: {Id}", ramAttr?.Id);
    Log.Information("  Накопитель ID: {Id}", storageAttr?.Id);
    Log.Information("  Видеокарта ID: {Id}", gpuAttr?.Id);
    Log.Information("  Диагональ ID: {Id}", diagonalAttr?.Id);
    Log.Information("  Разрешение ID: {Id}", resolutionAttr?.Id);
    Log.Information("  Частота обновления ID: {Id}", refreshRateAttr?.Id);
    Log.Information("  Цвет ID: {Id}", colorAttr?.Id);
    Log.Information("  ОС ID: {Id}", osAttr?.Id);

    var valuesAdded = 0;
    var errors = 0;

    foreach (var product in products)
    {
        var categoryName = product.Category?.Name ?? "";

        var existingCount = await context.ProductAttributeValues.CountAsync(v => v.ProductId == product.Id);
        if (existingCount > 0)
        {
            Log.Information("{Product} уже имеет {Count} характеристик, пропускаем", product.Name, existingCount);
            continue;
        }

        Log.Information("Добавление характеристик для: {Product} (Категория: {Category})", product.Name, categoryName);

        try
        {
            if (categoryName == "Компьютеры")
            {
                if (processorAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = processorAttr.Id,
                        Value = product.Name.Contains("Игровой") ? "Intel Core i7-13700K" : "Intel Core i5-13400"
                    });
                    valuesAdded++;
                    Log.Information("  + Процессор");
                }

                if (ramAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = ramAttr.Id,
                        Value = product.Name.Contains("Игровой") ? "32" : "16"
                    });
                    valuesAdded++;
                    Log.Information("  + Оперативная память");
                }

                if (storageAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = storageAttr.Id,
                        Value = product.Name.Contains("Игровой") ? "1024" : "512"
                    });
                    valuesAdded++;
                    Log.Information("  + Накопитель");
                }

                if (gpuAttr != null && product.Name.Contains("Игровой"))
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = gpuAttr.Id,
                        Value = "NVIDIA RTX 4070"
                    });
                    valuesAdded++;
                    Log.Information("  + Видеокарта");
                }

                if (colorAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = colorAttr.Id,
                        Value = "Черный"
                    });
                    valuesAdded++;
                    Log.Information("  + Цвет");
                }

                if (osAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = osAttr.Id,
                        Value = "Windows 11"
                    });
                    valuesAdded++;
                    Log.Information("  + Операционная система");
                }
            }
            else if (categoryName == "Ноутбуки")
            {
                if (processorAttr != null)
                {
                    string processor = product.Name.Contains("MacBook") ? "Apple M2 Pro" :
                                       product.Name.Contains("Gaming") ? "AMD Ryzen 9 7945HX" : "Intel Core i7-1360P";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = processorAttr.Id,
                        Value = processor
                    });
                    valuesAdded++;
                    Log.Information("  + Процессор");
                }

                if (ramAttr != null)
                {
                    string ram = product.Name.Contains("Gaming") ? "32" : 
                                product.Name.Contains("MacBook") ? "16" : "8";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = ramAttr.Id,
                        Value = ram
                    });
                    valuesAdded++;
                    Log.Information("  + Оперативная память");
                }

                if (storageAttr != null)
                {
                    string storage = product.Name.Contains("Gaming") ? "1024" :
                                    product.Name.Contains("MacBook") ? "512" : "256";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = storageAttr.Id,
                        Value = storage
                    });
                    valuesAdded++;
                    Log.Information("  + Накопитель");
                }

                if (gpuAttr != null && product.Name.Contains("Gaming"))
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = gpuAttr.Id,
                        Value = "NVIDIA RTX 4060"
                    });
                    valuesAdded++;
                    Log.Information("  + Видеокарта");
                }

                if (diagonalAttr != null)
                {
                    string diagonal = product.Name.Contains("15") ? "15.6" : 
                                      product.Name.Contains("13") ? "13.3" : "14";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = diagonalAttr.Id,
                        Value = diagonal
                    });
                    valuesAdded++;
                    Log.Information("  + Диагональ экрана");
                }

                if (colorAttr != null)
                {
                    string color = product.Name.Contains("MacBook") ? "Silver" : "Черный";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = colorAttr.Id,
                        Value = color
                    });
                    valuesAdded++;
                    Log.Information("  + Цвет");
                }
                
                if (osAttr != null)
                {
                    string os = product.Name.Contains("MacBook") ? "macOS" : "Windows 11";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = osAttr.Id,
                        Value = os
                    });
                    valuesAdded++;
                    Log.Information("  + Операционная система");
                }
            }
            else if (categoryName == "Телевизоры")
            {
                if (diagonalAttr != null)
                {
                    string diagonal = product.Name.Contains("32") ? "32" :
                                      product.Name.Contains("43") ? "43" :
                                      product.Name.Contains("50") ? "50" :
                                      product.Name.Contains("55") ? "55" :
                                      product.Name.Contains("65") ? "65" : "55";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = diagonalAttr.Id,
                        Value = diagonal
                    });
                    valuesAdded++;
                    Log.Information("  + Диагональ");
                }

                if (resolutionAttr != null)
                {
                    string resolution = product.Name.Contains("4K") ? "4K UHD (3840x2160)" : "Full HD (1920x1080)";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = resolutionAttr.Id,
                        Value = resolution
                    });
                    valuesAdded++;
                    Log.Information("  + Разрешение");
                }

                if (smartTvAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = smartTvAttr.Id,
                        Value = "Да"
                    });
                    valuesAdded++;
                    Log.Information("  + Smart TV");
                }
            }
            else if (categoryName == "Смартфоны")
            {
                if (processorAttr != null)
                {
                    string processor = product.Name.Contains("iPhone") ? "Apple A17 Pro" :
                                       product.Name.Contains("Galaxy") ? "Exynos 2400" : "Snapdragon 8 Gen 3";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = processorAttr.Id,
                        Value = processor
                    });
                    valuesAdded++;
                    Log.Information("  + Процессор");
                }

                if (ramAttr != null)
                {
                    string ram = product.Name.Contains("Pro") ? "8" : "6";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = ramAttr.Id,
                        Value = ram
                    });
                    valuesAdded++;
                    Log.Information("  + Оперативная память");
                }

                if (storageAttr != null)
                {
                    string storage = product.Name.Contains("Ultra") ? "512" : "128";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = storageAttr.Id,
                        Value = storage
                    });
                    valuesAdded++;
                    Log.Information("  + Накопитель");
                }

                if (diagonalAttr != null)
                {
                    string diagonal = product.Name.Contains("Ultra") ? "6.8" : "6.1";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = diagonalAttr.Id,
                        Value = diagonal
                    });
                    valuesAdded++;
                    Log.Information("  + Диагональ");
                }

                if (colorAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = colorAttr.Id,
                        Value = "Черный"
                    });
                    valuesAdded++;
                    Log.Information("  + Цвет");
                }

                if (osAttr != null)
                {
                    string os = product.Name.Contains("iPhone") ? "iOS 17" : "Android 14";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = osAttr.Id,
                        Value = os
                    });
                    valuesAdded++;
                    Log.Information("  + Операционная система");
                }
            }
            else if (categoryName == "Колонки")
            {
                if (processorAttr != null)
                {
                    string power = product.Name.Contains("Charge") ? "40 Вт" : "20 Вт";
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = processorAttr.Id,
                        Value = power
                    });
                    valuesAdded++;
                    Log.Information("  + Мощность");
                }

                if (colorAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = colorAttr.Id,
                        Value = "Черный"
                    });
                    valuesAdded++;
                    Log.Information("  + Цвет");
                }

                if (bluetoothAttr != null)
                {
                    context.ProductAttributeValues.Add(new ProductAttributeValue
                    {
                        ProductId = product.Id,
                        AttributeId = bluetoothAttr.Id,
                        Value = "Да"
                    });
                    valuesAdded++;
                    Log.Information("  + Bluetooth");
                }
            }

            await context.SaveChangesAsync();
            Log.Information("  => Добавлено {Count} характеристик для {Product}", valuesAdded, product.Name);
        }
        catch (Exception ex)
        {
            errors++;
            Log.Error(ex, "  ОШИБКА при добавлении характеристик для {Product}", product.Name);
        }
    }

    Log.Information("Всего добавлено {ValuesAdded} характеристик, ошибок: {Errors}", valuesAdded, errors);
}

static async Task ForceAddComputers(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ КОМПЬЮТЕРОВ");
    
    var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Компьютеры");
    var type = await context.Types.FirstOrDefaultAsync(t => t.Name == "Компьютер");
    var msi = await context.Brands.FirstOrDefaultAsync(b => b.Name == "MSI");
    var hp = await context.Brands.FirstOrDefaultAsync(b => b.Name == "HP");
    var dell = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Dell");
    var asus = await context.Brands.FirstOrDefaultAsync(b => b.Name == "ASUS");
    var lenovo = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Lenovo");
    
    if (category == null || type == null) return;
    
    var products = new[]
    {
        new Product { Name = "Игровой компьютер MSI", Price = 89999, CategoryId = category.Id, BrandId = msi?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Игровой компьютер RGB" },
        new Product { Name = "Офисный компьютер HP", Price = 44999, CategoryId = category.Id, BrandId = hp?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Офисный компьютер" },
        new Product { Name = "Домашний компьютер Dell", Price = 54999, CategoryId = category.Id, BrandId = dell?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Домашний компьютер" },
        new Product { Name = "Игровой компьютер ASUS", Price = 109999, CategoryId = category.Id, BrandId = asus?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Компьютер ASUS ROG" },
        new Product { Name = "Бюджетный компьютер Lenovo", Price = 34999, CategoryId = category.Id, BrandId = lenovo?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Бюджетный компьютер" }
    };
    
    foreach (var product in products)
        context.Products.Add(product);
    await context.SaveChangesAsync();
    
    Log.Information("Добавлено {Count} компьютеров", products.Length);
}

static async Task ForceAddLaptops(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ НОУТБУКОВ");
    
    var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Ноутбуки");
    var type = await context.Types.FirstOrDefaultAsync(t => t.Name == "Ноутбук");
    var apple = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Apple");
    var asus = await context.Brands.FirstOrDefaultAsync(b => b.Name == "ASUS");
    var lenovo = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Lenovo");
    var xiaomi = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Xiaomi");
    
    if (category == null || type == null) return;
    
    var products = new[]
    {
        new Product { Name = "Apple MacBook Air M2", Price = 119999, CategoryId = category.Id, BrandId = apple?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Ноутбук Apple MacBook Air M2" },
        new Product { Name = "ASUS VivoBook 15", Price = 54999, CategoryId = category.Id, BrandId = asus?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Ноутбук ASUS VivoBook 15" },
        new Product { Name = "Lenovo IdeaPad 3", Price = 44999, CategoryId = category.Id, BrandId = lenovo?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Ноутбук Lenovo IdeaPad 3" },
        new Product { Name = "Xiaomi RedmiBook 15", Price = 39999, CategoryId = category.Id, BrandId = xiaomi?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Ноутбук Xiaomi RedmiBook 15" },
        new Product { Name = "ASUS TUF Gaming", Price = 79999, CategoryId = category.Id, BrandId = asus?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Игровой ноутбук ASUS TUF" }
    };
    
    foreach (var product in products)
        context.Products.Add(product);
    await context.SaveChangesAsync();
    
    Log.Information("Добавлено {Count} ноутбуков", products.Length);
}

static async Task ForceAddTvs(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ ТЕЛЕВИЗОРОВ");
    
    var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Телевизоры");
    var type = await context.Types.FirstOrDefaultAsync(t => t.Name == "Телевизор");
    var samsung = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Samsung");
    var lg = await context.Brands.FirstOrDefaultAsync(b => b.Name == "LG");
    var sony = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Sony");
    var xiaomi = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Xiaomi");
    
    if (category == null || type == null) return;
    
    var products = new[]
    {
        new Product { Name = "Samsung QE55Q80AAUXCE", Price = 79999, CategoryId = category.Id, BrandId = samsung?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Телевизор Samsung QLED 4K" },
        new Product { Name = "LG OLED65C24LA", Price = 149999, CategoryId = category.Id, BrandId = lg?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Телевизор LG OLED 4K" },
        new Product { Name = "Sony KD-43X80K", Price = 54999, CategoryId = category.Id, BrandId = sony?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Телевизор Sony BRAVIA" },
        new Product { Name = "Xiaomi Mi TV P1 50", Price = 39999, CategoryId = category.Id, BrandId = xiaomi?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Телевизор Xiaomi 4K" },
        new Product { Name = "Samsung UE32T5300AUXCE", Price = 29999, CategoryId = category.Id, BrandId = samsung?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Телевизор Samsung HD" }
    };
    
    foreach (var product in products)
        context.Products.Add(product);
    await context.SaveChangesAsync();
    
    Log.Information("Добавлено {Count} телевизоров", products.Length);
}

static async Task ForceAddSmartphones(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ СМАРТФОНОВ");
    
    var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Смартфоны");
    var type = await context.Types.FirstOrDefaultAsync(t => t.Name == "Смартфон");
    var apple = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Apple");
    var samsung = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Samsung");
    var xiaomi = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Xiaomi");
    var google = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Google");
    var sony = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Sony");
    
    if (category == null || type == null) return;
    
    var products = new[]
    {
        new Product { Name = "Apple iPhone 15 Pro", Price = 119999, CategoryId = category.Id, BrandId = apple?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Смартфон Apple iPhone 15 Pro" },
        new Product { Name = "Samsung Galaxy S24", Price = 89999, CategoryId = category.Id, BrandId = samsung?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Смартфон Samsung Galaxy S24" },
        new Product { Name = "Xiaomi 14 Ultra", Price = 69999, CategoryId = category.Id, BrandId = xiaomi?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Смартфон Xiaomi 14 Ultra" },
        new Product { Name = "Google Pixel 8", Price = 64999, CategoryId = category.Id, BrandId = google?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Смартфон Google Pixel 8" },
        new Product { Name = "Sony Xperia 1 V", Price = 79999, CategoryId = category.Id, BrandId = sony?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Смартфон Sony Xperia 1 V" }
    };
    
    foreach (var product in products)
        context.Products.Add(product);
    await context.SaveChangesAsync();
    
    Log.Information("Добавлено {Count} смартфонов", products.Length);
}

static async Task ForceAddSpeakers(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ КОЛОНОК");
    
    var category = await context.Categories.FirstOrDefaultAsync(c => c.Name == "Колонки");
    var type = await context.Types.FirstOrDefaultAsync(t => t.Name == "Колонка");
    var jbl = await context.Brands.FirstOrDefaultAsync(b => b.Name == "JBL");
    var sony = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Sony");
    var sennheiser = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Sennheiser");
    var xiaomi = await context.Brands.FirstOrDefaultAsync(b => b.Name == "Xiaomi");
    
    if (category == null || type == null) return;
    
    var products = new[]
    {
        new Product { Name = "JBL Charge 5", Price = 12999, CategoryId = category.Id, BrandId = jbl?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Колонка JBL Charge 5" },
        new Product { Name = "JBL Flip 6", Price = 9999, CategoryId = category.Id, BrandId = jbl?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Колонка JBL Flip 6" },
        new Product { Name = "Sony SRS-XB43", Price = 14999, CategoryId = category.Id, BrandId = sony?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Колонка Sony" },
        new Product { Name = "Sennheiser AMBEO", Price = 29999, CategoryId = category.Id, BrandId = sennheiser?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Колонка Sennheiser AMBEO" },
        new Product { Name = "Xiaomi Mi Portable", Price = 3999, CategoryId = category.Id, BrandId = xiaomi?.Id ?? 1, TypeId = type.Id, IsActive = true, Description = "Колонка Xiaomi" }
    };
    
    foreach (var product in products)
        context.Products.Add(product);
    await context.SaveChangesAsync();
    
    Log.Information("Добавлено {Count} колонок", products.Length);
}

static async Task ForceAddAllProductImages(ApplicationDb context)
{
    Log.Information("ДОБАВЛЕНИЕ ИЗОБРАЖЕНИЙ ДЛЯ ВСЕХ ТОВАРОВ");

    var products = await context.Products
        .Include(p => p.Category)
        .OrderBy(p => p.Id)
        .ToListAsync();

    var imagesToAdd = new List<ProductImage>();

    var tvImageUrls = new[]
    {
        "/images/products/1.jpg",
        "/images/products/2.jpg",
        "/images/products/3.webp",
        "/images/products/4.webp",
        "/images/products/5.webp"
    };

    var laptopImageUrls = new[]
    {
        "/images/laptop/1.webp",
        "/images/laptop/2.webp",
        "/images/laptop/3.webp",
        "/images/laptop/4.webp",
        "/images/laptop/5.webp"
    };

    var computerImageUrls = new[]
    {
        "/images/computer/1.webp",
        "/images/computer/2.webp",
        "/images/computer/3.webp",
        "/images/computer/4.webp",
        "/images/computer/5.jpg"
    };

    var phoneImageUrls = new[]
    {
        "/images/phone/1.webp",
        "/images/phone/2.webp",
        "/images/phone/3.jpg",
        "/images/phone/4.jpg",
        "/images/phone/5.jpg"
    };

    var speakerImageUrls = new[]
    {
        "/images/speaker/1.webp",
        "/images/speaker/2.webp",
        "/images/speaker/3.webp",
        "/images/speaker/4.webp",
        "/images/speaker/5.webp"
    };

    int tvIndex = 0, laptopIndex = 0, computerIndex = 0, phoneIndex = 0, speakerIndex = 0;

    foreach (var product in products)
    {
        var categoryName = product.Category?.Name ?? "";
        string imageUrl = null;
        string altText = null;

        var existingImage = await context.ProductImages
            .FirstOrDefaultAsync(i => i.ProductId == product.Id && i.IsMain);

        if (existingImage != null)
        {
            Log.Information("  Изображение уже существует для: {Product}", product.Name);
            continue;
        }

        if (categoryName == "Телевизоры" && tvIndex < tvImageUrls.Length)
        {
            imageUrl = tvImageUrls[tvIndex];
            altText = $"{product.Name} - {product.Description}";
            tvIndex++;
        }
        else if (categoryName == "Ноутбуки" && laptopIndex < laptopImageUrls.Length)
        {
            imageUrl = laptopImageUrls[laptopIndex];
            altText = $"{product.Name} - {product.Description}";
            laptopIndex++;
        }
        else if (categoryName == "Компьютеры" && computerIndex < computerImageUrls.Length)
        {
            imageUrl = computerImageUrls[computerIndex];
            altText = $"{product.Name} - {product.Description}";
            computerIndex++;
        }
        else if (categoryName == "Смартфоны" && phoneIndex < phoneImageUrls.Length)
        {
            imageUrl = phoneImageUrls[phoneIndex];
            altText = $"{product.Name} - {product.Description}";
            phoneIndex++;
        }
        else if (categoryName == "Колонки" && speakerIndex < speakerImageUrls.Length)
        {
            imageUrl = speakerImageUrls[speakerIndex];
            altText = $"{product.Name} - {product.Description}";
            speakerIndex++;
        }

        if (!string.IsNullOrEmpty(imageUrl))
        {
            imagesToAdd.Add(new ProductImage
            {
                ProductId = product.Id,
                ImageUrl = imageUrl,
                AltText = altText,
                IsMain = true
            });
            Log.Information("  Добавлено изображение для: {Product} -> {ImageUrl}", product.Name, imageUrl);
        }
        else
        {
            Log.Warning("Нет изображения для: {Product} (категория: {Category})", product.Name, categoryName);
        }
    }

    if (imagesToAdd.Any())
    {
        context.ProductImages.AddRange(imagesToAdd);
        await context.SaveChangesAsync();
        Log.Information("Добавлено {Count} изображений", imagesToAdd.Count);
    }
    else
    {
        Log.Information("Новых изображений для добавления нет");
    }
}