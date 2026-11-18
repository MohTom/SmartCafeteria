using SmartCafeteria.Core.Models;

namespace SmartCafeteria.Data.Seed;

/// <summary>Default Sudanese cafeteria menu (meals + juices), seeded once on a fresh database.</summary>
public static class SeedData
{
    public static List<Product> GetSeedProducts()
    {
        return new List<Product>
        {
            // Grills
            new Product { Id = "seed-01", NameAr = "أقاشي لحم", NameEn = "Beef Agashi", DescriptionAr = "أقاشي لحم مشوي على الفحم", DescriptionEn = "Charcoal-grilled beef agashi", Price = 6500, Category = "Grills", Emoji = "🍢", Inventory = 30, MaxInventory = 50 },
            new Product { Id = "seed-02", NameAr = "أقاشي دجاج", NameEn = "Chicken Agashi", DescriptionAr = "أقاشي دجاج متبل ومشوي", DescriptionEn = "Marinated grilled chicken agashi", Price = 5500, Category = "Grills", Emoji = "🍢", Inventory = 30, MaxInventory = 50 },
            new Product { Id = "seed-03", NameAr = "شيش طاووق", NameEn = "Shish Tawook", DescriptionAr = "مكعبات دجاج مشوية مع الخضار", DescriptionEn = "Grilled chicken cubes with vegetables", Price = 5000, Category = "Grills", Emoji = "🍡", Inventory = 25, MaxInventory = 40 },
            new Product { Id = "seed-04", NameAr = "كفتة مشوية", NameEn = "Grilled Kofta", DescriptionAr = "كفتة لحم مشوية مع السلطة", DescriptionEn = "Grilled minced-beef kofta with salad", Price = 4500, Category = "Grills", Emoji = "🧆", Inventory = 25, MaxInventory = 40 },
            // Sandwiches
            new Product { Id = "seed-05", NameAr = "شاورما دجاج", NameEn = "Chicken Shawarma", DescriptionAr = "شاورما دجاج مع الثومية والمخلل", DescriptionEn = "Chicken shawarma with garlic sauce and pickles", Price = 3000, Category = "Sandwiches", Emoji = "🌯", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-06", NameAr = "شاورما لحم", NameEn = "Beef Shawarma", DescriptionAr = "شاورما لحم مع الطحينة", DescriptionEn = "Beef shawarma with tahini", Price = 3500, Category = "Sandwiches", Emoji = "🌯", Inventory = 30, MaxInventory = 60 },
            new Product { Id = "seed-07", NameAr = "ساندويتش طعمية", NameEn = "Falafel Sandwich", DescriptionAr = "طعمية مقرمشة مع السلطة والطحينة", DescriptionEn = "Crispy falafel with salad and tahini", Price = 1500, Category = "Sandwiches", Emoji = "🥙", Inventory = 50, MaxInventory = 80 },
            new Product { Id = "seed-08", NameAr = "ساندويتش بيض", NameEn = "Egg Sandwich", DescriptionAr = "بيض مقلي مع الجبنة والخضار", DescriptionEn = "Fried eggs with cheese and vegetables", Price = 1500, Category = "Sandwiches", Emoji = "🍳", Inventory = 40, MaxInventory = 60 },
            // Breakfast
            new Product { Id = "seed-09", NameAr = "فول مدمس", NameEn = "Foul Medammes", DescriptionAr = "فول مدمس بالزيت والليمون", DescriptionEn = "Fava beans with oil and lemon", Price = 2000, Category = "Breakfast", Emoji = "🍲", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-10", NameAr = "عدس", NameEn = "Lentils", DescriptionAr = "شوربة عدس سودانية", DescriptionEn = "Sudanese lentil stew", Price = 1500, Category = "Breakfast", Emoji = "🍲", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-11", NameAr = "طعمية (صحن)", NameEn = "Falafel Plate", DescriptionAr = "صحن طعمية مع الفول والسلطة", DescriptionEn = "Falafel plate with beans and salad", Price = 1500, Category = "Breakfast", Emoji = "🧆", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-12", NameAr = "فتة فول", NameEn = "Foul Fattah", DescriptionAr = "فتة فول بالعيش البلدي", DescriptionEn = "Fava bean fattah with baladi bread", Price = 2500, Category = "Breakfast", Emoji = "🥘", Inventory = 30, MaxInventory = 50 },
            // Juices
            new Product { Id = "seed-13", NameAr = "عصير فراولة", NameEn = "Strawberry Juice", DescriptionAr = "عصير فراولة طازج", DescriptionEn = "Fresh strawberry juice", Price = 2500, Category = "Juices", Emoji = "🍓", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-14", NameAr = "عصير مانجو", NameEn = "Mango Juice", DescriptionAr = "عصير مانجو طبيعي", DescriptionEn = "Natural mango juice", Price = 2500, Category = "Juices", Emoji = "🥭", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-15", NameAr = "عصير جوافة", NameEn = "Guava Juice", DescriptionAr = "عصير جوافة طازج", DescriptionEn = "Fresh guava juice", Price = 2000, Category = "Juices", Emoji = "🍈", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-16", NameAr = "ليمون بالنعناع", NameEn = "Mint Lemonade", DescriptionAr = "عصير ليمون بالنعناع المنعش", DescriptionEn = "Refreshing mint lemonade", Price = 1500, Category = "Juices", Emoji = "🍋", Inventory = 50, MaxInventory = 80 },
            new Product { Id = "seed-17", NameAr = "كركديه", NameEn = "Hibiscus Juice", DescriptionAr = "عصير كركديه بارد", DescriptionEn = "Cold hibiscus drink", Price = 1200, Category = "Juices", Emoji = "🌺", Inventory = 50, MaxInventory = 80 },
            new Product { Id = "seed-18", NameAr = "عرديب", NameEn = "Tamarind Juice", DescriptionAr = "عصير عرديب سوداني", DescriptionEn = "Sudanese tamarind drink", Price = 1200, Category = "Juices", Emoji = "🥤", Inventory = 50, MaxInventory = 80 },
            new Product { Id = "seed-19", NameAr = "كوكتيل طبقات", NameEn = "Layered Cocktail", DescriptionAr = "كوكتيل فواكه مشكلة بطبقات", DescriptionEn = "Mixed layered fruit cocktail", Price = 3000, Category = "Juices", Emoji = "🍹", Inventory = 30, MaxInventory = 50 },
            // Hot drinks
            new Product { Id = "seed-20", NameAr = "شاي سادة", NameEn = "Plain Tea", DescriptionAr = "شاي أحمر سوداني", DescriptionEn = "Sudanese red tea", Price = 500, Category = "HotDrinks", Emoji = "🍵", Inventory = 100, MaxInventory = 150 },
            new Product { Id = "seed-21", NameAr = "شاي لبن", NameEn = "Milk Tea", DescriptionAr = "شاي بالحليب", DescriptionEn = "Tea with milk", Price = 800, Category = "HotDrinks", Emoji = "☕", Inventory = 100, MaxInventory = 150 },
            new Product { Id = "seed-22", NameAr = "قهوة جبنة", NameEn = "Jebena Coffee", DescriptionAr = "قهوة سودانية بالجبنة مع البخور", DescriptionEn = "Sudanese jebena coffee", Price = 1000, Category = "HotDrinks", Emoji = "☕", Inventory = 80, MaxInventory = 120 },
            new Product { Id = "seed-23", NameAr = "زنجبيل", NameEn = "Ginger Drink", DescriptionAr = "مشروب الزنجبيل الساخن", DescriptionEn = "Hot ginger drink", Price = 800, Category = "HotDrinks", Emoji = "🫚", Inventory = 80, MaxInventory = 120 },
            // Cold drinks
            new Product { Id = "seed-24", NameAr = "ميلك شيك فانيليا", NameEn = "Vanilla Milkshake", DescriptionAr = "ميلك شيك بالفانيليا", DescriptionEn = "Vanilla milkshake", Price = 3000, Category = "ColdDrinks", Emoji = "🥤", Inventory = 30, MaxInventory = 50 },
            new Product { Id = "seed-25", NameAr = "آيس كوفي", NameEn = "Iced Coffee", DescriptionAr = "قهوة مثلجة بالحليب", DescriptionEn = "Iced coffee with milk", Price = 2500, Category = "ColdDrinks", Emoji = "🧊", Inventory = 30, MaxInventory = 50 },
            // Desserts
            new Product { Id = "seed-26", NameAr = "لقيمات", NameEn = "Luqaimat", DescriptionAr = "لقيمات مقرمشة بالعسل", DescriptionEn = "Crispy dumplings with honey", Price = 1500, Category = "Desserts", Emoji = "🍩", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-27", NameAr = "بسبوسة", NameEn = "Basbousa", DescriptionAr = "بسبوسة بالقشطة", DescriptionEn = "Semolina cake with cream", Price = 1500, Category = "Desserts", Emoji = "🍰", Inventory = 40, MaxInventory = 60 },
            new Product { Id = "seed-28", NameAr = "كنافة", NameEn = "Kunafa", DescriptionAr = "كنافة بالجبنة", DescriptionEn = "Cheese kunafa", Price = 3000, Category = "Desserts", Emoji = "🍮", Inventory = 25, MaxInventory = 40 },
            // Pies
            new Product { Id = "seed-29", NameAr = "سمبوسة لحم", NameEn = "Meat Sambusa", DescriptionAr = "سمبوسة مقرمشة باللحم", DescriptionEn = "Crispy meat sambusa", Price = 1200, Category = "SavoryPies", Emoji = "🥟", Inventory = 50, MaxInventory = 80 },
            new Product { Id = "seed-30", NameAr = "فطيرة نوتيلا", NameEn = "Nutella Pastry", DescriptionAr = "فطيرة حلوة بالنوتيلا", DescriptionEn = "Sweet pastry with Nutella", Price = 2500, Category = "SweetPies", Emoji = "🥧", Inventory = 30, MaxInventory = 50 },
        };
    }
}
