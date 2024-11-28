using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using ShadowShop.CatalogDb;
using Stripe;

namespace ShadowShop.CatalogInitializer;

public class Initializer(IServiceProvider serviceProvider, ILogger<Initializer> logger)
    : BackgroundService
{
    public const string ActivitySourceName = "Migrations";

    private readonly ActivitySource _activitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var stripeClient = scope.ServiceProvider.GetRequiredService<IStripeClient>();

        var sw = Stopwatch.StartNew();
        await InitializeDatabaseAsync(dbContext, cancellationToken);
        await SeedAsync(dbContext, stripeClient, cancellationToken);
        
        logger.LogInformation("Database initialization completed after {ElapsedMilliseconds}ms", sw.ElapsedMilliseconds);
    }

    private async Task InitializeDatabaseAsync(CatalogDbContext dbContext, CancellationToken cancellationToken)
    {
        using var activity = _activitySource.StartActivity();
        await dbContext.Database.EnsureCreatedAsync(cancellationToken);
    }

    private async Task SeedAsync(CatalogDbContext dbContext, IStripeClient stripeClient, CancellationToken cancellationToken)
    {
        logger.LogInformation("Seeding database");

        static List<CatalogBrand> GetPreconfiguredCatalogBrands()
        {
            return [
                new() { Brand = "Scary" },
                new() { Brand = "Creepy" },
                new() { Brand = "Ghoulish" },
                new() { Brand = "Terrifying" },
                new() { Brand = "Frightening" }
            ];
        }

        static List<CatalogType> GetPreconfiguredCatalogTypes()
        {
            return [
                new() { Type = "Pirates" },
                new() { Type = "Vampires" },
                new() { Type = "Skeletons" },
                new() { Type = "Scarecrows" },
                new() { Type = "Witches" }
            ];
        }

        static List<CatalogItem> GetPreconfiguredItems(DbSet<CatalogBrand> catalogBrands, DbSet<CatalogType> catalogTypes)
        {
            var scary = catalogBrands.First(b => b.Brand == "Scary");
            var creepy = catalogBrands.First(b => b.Brand == "Creepy");

            var pirates = catalogTypes.First(c => c.Type == "Pirates");
            var skeletons = catalogTypes.First(c => c.Type == "Skeletons");
            var scarecrows = catalogTypes.First(c => c.Type == "Scarecrows");
            var vampires = catalogTypes.First(c => c.Type == "Vampires");
            var witches = catalogTypes.First(c => c.Type == "Witches");

            return [
                new() { CatalogType = pirates, CatalogBrand = scary, AvailableStock = 100, Description = "Dark Pirate Halloween costume", Name = "Dark Pirate", Price = 95.5M, PictureFileName = "1.png" },
                new() { CatalogType = witches, CatalogBrand = scary, AvailableStock = 100, Description = "Evil Witch Halloween costume", Name = "Evil Witch", Price= 86.50M, PictureFileName = "2.png" },
                new() { CatalogType = skeletons, CatalogBrand = creepy, AvailableStock = 100, Description = "Red Skeleton Hoodie Halloween costume", Name = "Red Skeleton Hoodie", Price = 120, PictureFileName = "3.png" },
                new() { CatalogType = scarecrows, CatalogBrand = scary, AvailableStock = 100, Description = "Ghoulish Scarecrow Halloween costume", Name = "Ghoulish Scarecrow", Price = 96, PictureFileName = "4.png" },
                new() { CatalogType = witches, CatalogBrand = creepy, AvailableStock = 100, Description = "Baba Yaga Halloween costume", Name = "Baba Yaga", Price = 160.5M, PictureFileName = "5.png" },
                new() { CatalogType = pirates, CatalogBrand = scary, AvailableStock = 100, Description = "Cutthroat Pirate Halloween costume", Name = "Cutthroat Pirate", Price = 72, PictureFileName = "6.png" },
                new() { CatalogType = skeletons, CatalogBrand = creepy, AvailableStock = 100, Description = "Undead Soul Halloween costume", Name = "Undead Soul", Price = 78, PictureFileName = "7.png" },
                new() { CatalogType = skeletons, CatalogBrand = creepy, AvailableStock = 100, Description = "Pumpkin Patch Spirit Halloween costume", Name = "Pumpkin Patch Spirit", Price = 108.5M, PictureFileName = "8.png" },
                new() { CatalogType = skeletons, CatalogBrand = creepy, AvailableStock = 100, Description = "Lady of the Graveyard Halloween costume", Name = "Lady of the Graveyard", Price = 92, PictureFileName = "9.png" },
                new() { CatalogType = vampires, CatalogBrand = scary, AvailableStock = 100, Description = "Lord of the Vampires Halloween costume", Name = "Lord of the Vampires", Price = 89.99M, PictureFileName = "10.png" },
                new() { CatalogType = scarecrows, CatalogBrand = scary, AvailableStock = 100, Description = "Original Patch Halloween costume", Name = "Original Patch", Price = 95.2M, PictureFileName = "11.png" },
                new() { CatalogType = scarecrows, CatalogBrand = creepy, AvailableStock = 100, Description = "Mrs. Patch Halloween costume", Name = "Mrs. Patch", Price = 90.2M, PictureFileName = "12.png" }
            ];
        }

        if (!dbContext.CatalogBrands.Any())
        {
            var brands = GetPreconfiguredCatalogBrands();
            await dbContext.CatalogBrands.AddRangeAsync(brands, cancellationToken);

            logger.LogInformation("Seeding {CatalogBrandCount} catalog brands", brands.Count);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!dbContext.CatalogTypes.Any())
        {
            var types = GetPreconfiguredCatalogTypes();
            await dbContext.CatalogTypes.AddRangeAsync(types, cancellationToken);

            logger.LogInformation("Seeding {CatalogTypeCount} catalog item types", types.Count);

            await dbContext.SaveChangesAsync(cancellationToken);
        }

        if (!dbContext.CatalogItems.Any())
        {
            var items = GetPreconfiguredItems(dbContext.CatalogBrands, dbContext.CatalogTypes);
            await dbContext.CatalogItems.AddRangeAsync(items, cancellationToken);

            logger.LogInformation("Seeding {CatalogItemCount} catalog items", items.Count);
            
            await SeedStripeAsync(cancellationToken);
            
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return;

        // Stripe
        async Task<bool> AnyExistingProductsAsync(CancellationToken cancelToken)
        {
            var listOptions = new ProductListOptions { Active = true, Limit = 1 };
            var productService = new ProductService(stripeClient);

            var existingProducts = await productService.ListAsync(listOptions, cancellationToken: cancelToken);
            return existingProducts.Any();
        }

        async Task CreateStripeProductAsync(
            CatalogItem catalogItem, CancellationToken cancelToken)
        {
            var productService = new ProductService(stripeClient);
            var priceService = new PriceService(stripeClient);
            var fileService = new FileService(stripeClient);
            var fileLinkService = new FileLinkService(stripeClient);

            // Store product image in Stripe
            var imagePath = Path.Combine(Directory.GetCurrentDirectory(), "../ShadowShop.CatalogService/Images",
                catalogItem.PictureFileName);
            await using var imageStream = System.IO.File.OpenRead(imagePath);

            logger.LogInformation("Storing image located in {ImagePath} in Stripe", imagePath);
            var createdFile = await fileService.CreateAsync(
                new FileCreateOptions { File = imageStream, Purpose = FilePurpose.BusinessLogo },
                cancellationToken: cancelToken);

            var fileLink = await fileLinkService.CreateAsync(new FileLinkCreateOptions() { File = createdFile.Id },
                cancellationToken: cancelToken);

            // Create the product
            var product = await productService.CreateAsync(
                new ProductCreateOptions
                {
                    Name = catalogItem.Name,
                    Description = catalogItem.Description,
                    Images = [fileLink.Url],
                    Metadata = new Dictionary<string, string>
                    {
                        { "catalog.type", catalogItem.CatalogType.Type },
                        { "catalog.brand", catalogItem.CatalogBrand.Brand }
                    }
                }, cancellationToken: cancelToken);
            
            // Create the price
            var price = await priceService.CreateAsync(
                new PriceCreateOptions
                {
                    Product = product.Id, Currency = "usd", UnitAmount = (long)(catalogItem.Price * 100),
                }, cancellationToken: cancelToken);
            
            catalogItem.PictureUri = fileLink.Url;
            catalogItem.StripePriceId = price.Id;
        }

        async Task SeedStripeAsync(CancellationToken cancelToken)
        {
            if (!await AnyExistingProductsAsync(cancelToken))
            {
                foreach (var catalogItem in dbContext.CatalogItems.Local)
                {
                    await CreateStripeProductAsync(catalogItem, cancelToken);
                }
            }
        }
    }
}