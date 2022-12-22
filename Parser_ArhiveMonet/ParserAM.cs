using Dapper;
using HtmlAgilityPack;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.Drawing.Imaging;
using System.Net;

namespace Parser_ArhiveMonet
{
    public class ParserAM
    {
        private readonly Uri _uriWebSite;
        private readonly string _imagesFolder;
        private readonly string _fileCatalogs;
        private readonly string _connectionString;

        public int IdCoin { get; set; }
        public List<string> CatalogsUrl { get; set; }

        //текущие позиции
        private string _lastCatalogChecking;
        private int _indexCurCatalog;

        public ParserAM(Uri uriWebSite, string fileCatalogs, string imagesFolder, string connectionString)
        {
            _uriWebSite = uriWebSite;
            _imagesFolder = imagesFolder;
            _fileCatalogs = fileCatalogs;
            _connectionString = connectionString;
            IdCoin = GetLastId();
        }

        public async Task<bool> RunAsync()
        {
            try
            {
                //получаем ссылки на все каталоги
                CatalogsUrl = new();
                Console.WriteLine("Проверяем данные о каталогах");
                if (false)
                {
                    CatalogsUrl = GetCatalogsUrl(_uriWebSite);
                    SaveInFile(CatalogsUrl, _fileCatalogs);
                    Console.WriteLine("Обновление каталогов прошло успешно");
                }
                else
                {
                    CatalogsUrl = File.ReadAllLines(_fileCatalogs).ToList();
                    Console.WriteLine("Для обновления каталогов в странах еще не прошло время, берем данные из файла");
                }

                //получить ссылки на монеты из каталога
                _indexCurCatalog = 0;
                Console.WriteLine("Получаем монеты из каталогов:");
                foreach (var catalog in CatalogsUrl)
                {

                    _lastCatalogChecking = catalog;
                    Console.WriteLine($"Берем данные из каталога - {_lastCatalogChecking}");
                    List<DirtyCoin> coins = GetCoins(GetCoinsUrl(catalog));
                    await SaveCoinsSqlAsync(coins);
                    _indexCurCatalog++;
                    SaveChanges();
                }
            }
            catch(Exception e)
            {
                Console.WriteLine(e.StackTrace);
                Console.WriteLine(e.Message);
                Console.WriteLine($"Остановился на каталоге: {_lastCatalogChecking}");
                SaveChanges();
                return false;
            }
            return true;
        }


        private List<string> GetCatalogsUrl(Uri uriWebSite)
        {
            List<string> urlsCatalog = new();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uriWebSite);
            request.Headers.Add(HttpRequestHeader.UserAgent, "PostmanRuntime/7.29.2");
            using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using Stream stream = response.GetResponseStream();

            HtmlDocument doc = new();
            doc.Load(stream);

            var result = doc.DocumentNode.SelectNodes("//a[contains(@class, 'nominal')]");

            if (result != null)
            {
                foreach (var item in result)
                {
                    urlsCatalog.Add(_uriWebSite.Scheme + "://" + _uriWebSite.Host + item.GetAttributeValue("href", ""));
                }
            }

            return urlsCatalog;
        }

        private List<string> GetCoinsUrl(string catalog)
        {
            List<string> coinsDiv = new();

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(catalog);
            request.Headers.Add(HttpRequestHeader.UserAgent, "PostmanRuntime/7.29.2");
            using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
            using Stream stream = response.GetResponseStream();

            HtmlDocument doc = new();
            doc.Load(stream);

            var result = doc.DocumentNode.SelectNodes("//table[contains(@class, 'coins')]/tbody//tr/td[1]/a");

            if (result != null)
            {
                foreach (var item in result)
                {
                    coinsDiv.Add(_uriWebSite.Scheme + "://" + _uriWebSite.Host + item.GetAttributeValue("href", ""));
                }
            }
            Thread.Sleep(1000);
            return coinsDiv;
        }

        private List<DirtyCoin> GetCoins(List<string> urlsCoin)
        {
            List<DirtyCoin> coins = new();

            foreach (var uriItem in urlsCoin)
            {
                Console.WriteLine($"Берем данные о монете: {uriItem}");
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(uriItem);
                request.Headers.Add(HttpRequestHeader.UserAgent, "PostmanRuntime/7.29.2");
                try
                {
                    using HttpWebResponse response = (HttpWebResponse)request.GetResponse();
                    using Stream stream = response.GetResponseStream();

                    HtmlDocument doc = new();
                    doc.Load(stream);

                    DirtyCoin dirtyCoin = new();

                    for (int index = 1; index < 10; index++)
                    {
                        switch (doc.DocumentNode.SelectNodes($"//ul[contains(@class, 'catnums')]/li[{index}]/strong")?.FirstOrDefault()?.InnerHtml)
                        {
                            case "Биткин":
                                dirtyCoin.Bitkin = doc.DocumentNode.SelectNodes($"//ul[contains(@class, 'catnums')]/li[{index}]/text()")?.FirstOrDefault()?.InnerHtml;
                                break;
                            case "Конрос":
                                dirtyCoin.Conros = doc.DocumentNode.SelectNodes($"//ul[contains(@class, 'catnums')]/li[{index}]/text()")?.FirstOrDefault()?.InnerHtml;
                                break;
                            default:
                                break;
                        }

                        switch (doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[1]")?.FirstOrDefault()?.InnerHtml)
                        {
                            case "Номинал":
                                dirtyCoin.Denomination = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[2]")?.FirstOrDefault()?.InnerHtml;
                                break;
                            case "Качество":
                                dirtyCoin.Quality = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[2]")?.FirstOrDefault()?.InnerHtml;
                                break;
                            case "Металл, проба":
                                dirtyCoin.Material = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[2]")?.FirstOrDefault()?.InnerHtml;
                                break;
                            case "Масса общая":
                                dirtyCoin.Weight = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[2]")?.FirstOrDefault()?.InnerHtml;
                                break;
                            case "Диаметр":
                                dirtyCoin.Diameter = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[2]")?.FirstOrDefault()?.InnerHtml;
                                break;
                            case "Тираж":
                                dirtyCoin.Circulation = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/table/tbody/tr[{index}]/th[2]")?.FirstOrDefault()?.InnerHtml;
                                break;
                            default:
                                break;
                        }

                        switch (doc.DocumentNode.SelectNodes($"//table[contains(@class, 'coins')]/tbody/tr[{index}]/th")?.FirstOrDefault()?.InnerHtml)
                        {
                            case "Гурт:":
                                dirtyCoin.Gurt = doc.DocumentNode.SelectNodes($"//table[contains(@class, 'coins')]/tbody/tr[{index}]/td")?.FirstOrDefault()?.InnerHtml;
                                break;
                            default:
                                break;
                        }
                    }

                    dirtyCoin.Obverse = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/text()[2]")?.FirstOrDefault()?.InnerHtml;
                    dirtyCoin.Reverse = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/text()[3]")?.FirstOrDefault()?.InnerHtml;

                    var imgFront = doc.DocumentNode.SelectNodes($"//*[contains(@id,'aboutus')]/div/div[2]/div/div[1]/div[1]/a/img")?.FirstOrDefault();
                    if (imgFront != null)
                    {
                        var imgFrontUrl = @$"{_imagesFolder}\{IdCoin}_front.webp";
                        using (WebClient client = new WebClient())
                        {
                            client.Headers.Add(HttpRequestHeader.UserAgent, "PostmanRuntime/7.29.2");
                            using (Stream streamImg = client.OpenRead("https://" + _uriWebSite.Host + imgFront?.GetAttributeValue("src", "")))
                            {
                                Bitmap bitmap = new Bitmap(streamImg);
                                if (bitmap != null)
                                {
                                    Image image = bitmap;
                                    image.Save(imgFrontUrl, ImageFormat.Webp);
                                }
                            }
                        }
                        dirtyCoin.ImgFrontUrl = imgFrontUrl;
                    }

                    var imgBack = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/div[1]/div[2]/a/img")?.FirstOrDefault();
                    if (imgBack != null)
                    {
                        var imgBackUrl = @$"{_imagesFolder}\{IdCoin}_back.webp";
                        using (WebClient client = new())
                        {
                            client.Headers.Add(HttpRequestHeader.UserAgent, "PostmanRuntime/7.29.2");
                            using (Stream streamImg = client.OpenRead("https://" + _uriWebSite.Host + imgFront?.GetAttributeValue("src", "")))
                            {
                                Bitmap bitmap = new(streamImg);
                                if (bitmap != null)
                                {
                                    Image image = bitmap;
                                    image.Save(imgBackUrl, ImageFormat.Webp);
                                }
                            }
                        }
                        dirtyCoin.ImgBackUrl = imgBackUrl;
                    }
                    dirtyCoin.Year = doc.DocumentNode.SelectNodes($"//*[contains(@id, 'aboutus')]/div/div[2]/div/p[1]/em")?.FirstOrDefault()?.InnerHtml;

                    coins.Add(dirtyCoin);
                    IdCoin++;

                }
                catch (Exception)
                {
                    Console.WriteLine($"Ошибка при парсинге монеты: {uriItem}");
                }
                Thread.Sleep(2000);
            }
            return coins;
        }

        private async Task SaveCoinsSqlAsync(List<DirtyCoin> coins)
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                foreach (var coin in coins)
                {
                    var param = new DynamicParameters();
                    param.Add("Bitkin", coin.Bitkin);
                    param.Add("Conros", coin.Conros);
                    param.Add("Denomination", coin.Denomination);
                    param.Add("Quality", coin.Quality);
                    param.Add("Material", coin.Material);
                    param.Add("Weight", coin.Weight);
                    param.Add("Diameter", coin.Diameter);
                    param.Add("Circulation", coin.Circulation);
                    param.Add("Gurt", coin.Gurt);
                    param.Add("Year", coin.Year);
                    param.Add("Obverse", coin.Obverse);
                    param.Add("Reverse", coin.Reverse);
                    param.Add("ImgFrontUrl", coin.ImgFrontUrl);
                    param.Add("ImgBackUrl", coin.ImgBackUrl);

                    var querry = "INSERT INTO DirtyCoin " +
                        "([Bitkin], [Conros], [Denomination], [Quality], [Material], [Weight], [Diameter], [Circulation], [Gurt], [Year], [Obverse], [Reverse], [ImgFrontUrl], [ImgBackUrl]) " +
                        "VALUES " +
                        "(@Bitkin, @Conros, @Denomination, @Quality, @Material, @Weight, @Diameter, @Circulation, @Gurt, @Year, @Obverse, @Reverse, @ImgFrontUrl, @ImgBackUrl);";
                    await db.QueryAsync(querry, param);
                }
            }
            Console.WriteLine($"Данные о монетах из каталога {_lastCatalogChecking} сохранены в Базу Данных ParserColnect");
            Console.WriteLine();
        }

        private void SaveInFile(List<string> urls, string fileName)
        {
            using (StreamWriter sw = new StreamWriter(fileName))
            {
                foreach (var url in urls)
                {
                    sw.WriteLine(url.ToString());
                }
            }
        }

        private int GetLastId()
        {
            using (IDbConnection db = new SqlConnection(_connectionString))
            {
                var querry = "SELECT max(id) FROM DirtyCoin";
                try
                {
                    return db.Query<int>(querry).FirstOrDefault() + 1;
                }
                catch (Exception e)
                {
                    return 1;
                }
            }
        }

        private void SaveChanges()
        {
            try
            {
                SaveInFile(CatalogsUrl.GetRange(_indexCurCatalog, CatalogsUrl.Count - _indexCurCatalog), _fileCatalogs);
            }
            catch(Exception)
            {
                Console.WriteLine("Не удалось сохранить изменения");
            }
        }
    } 
}
