using Parser_ArhiveMonet;

var uri = new Uri("https://www.arhivmonet.ru/#russia1");
var catalogs = @"C:\Users\ROKO000000005\Desktop\Parser_ArhiveMonet\Catalogs.txt";
var connectionString = @"Data Source=LEVAN\SQLEXPRESS;Initial Catalog=ParserAM; Integrated Security=true;";
var imagesForlder = @"C:\Users\ROKO000000005\Desktop\Parser_ArhiveMonet\Images";

ParserAM parser = new(uri,catalogs,imagesForlder,connectionString);

if (parser.RunAsync().Result)
{
    Console.WriteLine("Парсер успешно отработал");
}
else
{
    Console.WriteLine("Парсер отработал с ошибками, посмотрите ошибки выше");
}
Console.ReadLine();