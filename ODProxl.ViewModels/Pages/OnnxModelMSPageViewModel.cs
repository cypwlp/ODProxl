using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using ODProxl.Services;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Pages
{
    public class OnnxModelMSPageViewModel : BindableBase, INavigationAware
    {
        private readonly string _baseUrl = "http://interior.topmix.net/info/system/software/ODProxl/OnnxModels/";
        private readonly string _globalClassesUrl = "http://interior.topmix.net/info/system/software/ODProxl/classes.txt"; // 全域共用
        private readonly HttpClient _httpClient;
        private readonly IDataService _dataService;
        private readonly IOnnxModelAnalyzer _onnxModelAnalyzer;
        private bool _isLoading;
        private List<FileSystemItem> _allItems = new();
        private string _searchText = string.Empty;
        private LoginInfo? loginInfo;
        private OnnxAnalysisResult? _modelInfo;
        private ObservableCollection<ClassInfo> _classInfos;
        private readonly HttpClient _sharedHttpClient;

        public ObservableCollection<ClassInfo> ClassInfos
        {
            get => _classInfos;
            set => SetProperty(ref _classInfos, value);
        }
        public OnnxAnalysisResult? ModelInfo
        {
            get => _modelInfo;
            set => SetProperty(ref _modelInfo, value);
        }
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }
        public LoginInfo? LoginInfo
        {
            get => loginInfo;
            set => SetProperty(ref loginInfo, value);
        }
        public string SearchText
        {
            get => _searchText;
            set => SetProperty(ref _searchText, value);
        }
        public ObservableCollection<FileSystemItem> Items { get; } = new();
        public DelegateCommand<FileSystemItem>? ShowDetailsCommand { get; private set; }
        public DelegateCommand? SearchCommand { get; private set; }

        public OnnxModelMSPageViewModel(IDataService dataService, IOnnxModelAnalyzer onnxModelAnalyzer, HttpClient httpClient)
        {
            _dataService = dataService;
            _onnxModelAnalyzer = onnxModelAnalyzer;
            _httpClient = httpClient;
            SearchCommand = new DelegateCommand(FilterItems);
            ShowDetailsCommand = new DelegateCommand<FileSystemItem>(ShowDetails);
            _sharedHttpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            var byteArray = Encoding.ASCII.GetBytes("Administrator:wingfat@790811");
            _sharedHttpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
            _sharedHttpClient.DefaultRequestHeaders.UserAgent.TryParseAdd("ODProxl/1.0");
        }

        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }

        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoginInfo = navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
            await LoadModelsFromServerAsync();
        }

        private async Task LoadModelsFromServerAsync()
        {
            IsLoading = true;
            Items.Clear();
            _allItems.Clear();
            try
            {
                string html = await _httpClient.GetStringAsync(_baseUrl);
                var regex = new Regex(
                    @"(\d{4}/\d{1,2}/\d{1,2})\s+(\d{1,2}:\d{2})\s+(\d+)\s+<A\s+HREF=""([^""]+)"">([^<]+)</A>",
                    RegexOptions.IgnoreCase | RegexOptions.Multiline);
                var matches = regex.Matches(html);
                var tempItems = new List<FileSystemItem>();
                int index = 1;
                foreach (Match match in matches)
                {
                    if (match.Groups.Count < 6) continue;
                    string dateStr = match.Groups[1].Value;
                    string timeStr = match.Groups[2].Value;
                    string sizeStr = match.Groups[3].Value;
                    string relativeUrl = match.Groups[4].Value;
                    string fileName = match.Groups[5].Value.Trim();
                    if (!fileName.EndsWith(".onnx", StringComparison.OrdinalIgnoreCase))
                        continue;
                    string fullUrl = relativeUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                        ? relativeUrl
                        : new Uri(new Uri(_baseUrl), relativeUrl).ToString();
                    long.TryParse(sizeStr, out long sizeBytes);
                    DateTime lastModified = DateTime.Now;
                    if (DateTime.TryParse($"{dateStr} {timeStr}", out DateTime parsed))
                        lastModified = parsed;
                    var item = new FileSystemItem
                    {
                        Index = index++,
                        Name = fileName,
                        FullPath = fullUrl,
                        Size = sizeBytes > 0 ? sizeBytes : null,
                        LastModified = lastModified
                    };
                    item.EnabledChanged += OnItemEnabledChanged;
                    tempItems.Add(item);
                    var classesUrl = fullUrl.Replace(".onnx", "_classes.txt", StringComparison.OrdinalIgnoreCase);
                    try
                    {
                        var classesText = await _httpClient.GetStringAsync(classesUrl);
                        item.ModelClasses = classesText
                            .Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(s => s.Trim())
                            .Where(s => !string.IsNullOrWhiteSpace(s))
                            .ToList();
                    }
                    catch { }
                }
                _allItems = tempItems.OrderBy(i => i.Name).ToList();
                foreach (var item in _allItems)
                {
                    Items.Add(item);
                }
                await GetUserEnableModelAsync();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"載入失敗: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async void OnItemEnabledChanged(object? sender, EventArgs e)
        {
            if (sender is not FileSystemItem selectedItem) return;
            if (IsLoading) return;
            await SetEnabledModelAsync(selectedItem);
        }

        public async Task SetEnabledModelAsync(FileSystemItem selectedItem)
        {
            if (selectedItem == null || LoginInfo?.LoginName == null) return;
            IsLoading = true;
            try
            {
                foreach (var item in Items)
                {
                    if (item != selectedItem && item.IsEnabled)
                    {
                        item.IsEnabled = false;
                    }
                }
                await SaveSelectedModelAsync(selectedItem);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 設定啟用模型失敗: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task SaveSelectedModelAsync(FileSystemItem selectedItem)
        {
            ObservableCollection<ClassInfo>? resultList = null;
            string? localModelPath = null;
            try
            {
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModelsCache");
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);
                localModelPath = Path.Combine(cacheDir, selectedItem.Name);
                bool needDownload = true;
                if (File.Exists(localModelPath))
                {
                    var fileInfo = new FileInfo(localModelPath);
                    if (fileInfo.Length > 0)
                    {
                        needDownload = false;
                    }
                    else
                    {
                        File.Delete(localModelPath);
                    }
                }
                if (needDownload)
                {
                    using (var httpClient = new HttpClient())
                    {
                        var bytes = await httpClient.GetByteArrayAsync(selectedItem.FullPath);
                        await File.WriteAllBytesAsync(localModelPath, bytes);
                    }
                }
                ModelInfo = await _onnxModelAnalyzer.AnalyzeAsync(localModelPath, deepAnalysis: true);
                string? raw = ModelInfo?.CustomMetadata?.GetValueOrDefault("names");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    try
                    {
                        var dict = JsonSerializer.Deserialize<Dictionary<int, string>>(raw);
                        if (dict != null)
                        {
                            resultList = new ObservableCollection<ClassInfo>();
                            foreach (var kv in dict)
                            {
                                resultList.Add(new ClassInfo { Suffix = kv.Key, ClassName = kv.Value });
                            }
                            ClassInfos = resultList;
                        }
                    }
                    catch { }
                    if (resultList == null)
                    {
                        string rawTrimmed = raw.Trim();
                        if (rawTrimmed.StartsWith("{") && rawTrimmed.EndsWith("}"))
                            rawTrimmed = rawTrimmed.Substring(1, rawTrimmed.Length - 2);
                        var parts = rawTrimmed.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                        resultList = new ObservableCollection<ClassInfo>();
                        foreach (var part in parts)
                        {
                            var trimmedPart = part.Trim();
                            var keyValue = trimmedPart.Split(new[] { ':' }, StringSplitOptions.RemoveEmptyEntries);
                            if (keyValue.Length == 2 &&
                                int.TryParse(keyValue[0].Trim(), out int suffix))
                            {
                                string className = keyValue[1].Trim();
                                if (className.StartsWith("\"") && className.EndsWith("\""))
                                    className = className.Substring(1, className.Length - 2);
                                else if (className.StartsWith("'") && className.EndsWith("'"))
                                    className = className.Substring(1, className.Length - 2);
                                if (className.EndsWith("}"))
                                    className = className.TrimEnd('}');
                                resultList.Add(new ClassInfo
                                {
                                    Suffix = suffix,
                                    ClassName = className
                                });
                            }
                        }
                        ClassInfos = resultList;
                    }
                }
                if (resultList != null && resultList.Any())
                {
                    string classesContent = GenerateClassesFileContent(resultList);
                    await UploadClassesFileAsync(selectedItem.Name, classesContent);
                }
                else
                {
                    await UploadClassesFileAsync(selectedItem.Name, string.Empty);
                }

                string mergeSql = @"
            MERGE INTO sys_models AS target
            USING (SELECT @LoginName AS model_userAccount) AS source
            ON target.model_userAccount = source.model_userAccount
            WHEN MATCHED THEN
                UPDATE SET
                    model_name = @ModelName,
                    model_path = @ModelPath
            WHEN NOT MATCHED THEN
                INSERT (model_userAccount, model_name, model_path)
                VALUES (@LoginName, @ModelName, @ModelPath)
            OUTPUT INSERTED.model_id;";
                var parameters = new[]
                {
                    new SqlParameter("@LoginName", LoginInfo!.LoginName),
                    new SqlParameter("@ModelName", selectedItem.Name ?? ""),
                    new SqlParameter("@ModelPath", selectedItem.FullPath ?? "")
                };
                string? resultId = await _dataService.ScalarParamAsync("ODProxl", mergeSql, parameters);
                if (!int.TryParse(resultId, out int modelId))
                {
                    return;
                }
                if (resultList != null && resultList.Any())
                {
                    string deleteSql = "DELETE FROM sys_model_classes WHERE class_model_id = @ModelId";
                    await _dataService.ExecParamAsync("ODProxl", deleteSql, new SqlParameter("@ModelId", modelId));
                    string insertSql = @"
                INSERT INTO sys_model_classes (class_model_id, class_suffix, class_name)
                VALUES (@ModelId, @ClassSuffix, @ClassName)";
                    foreach (var classInfo in resultList)
                    {
                        var classParams = new[]
                        {
                            new SqlParameter("@ModelId", modelId),
                            new SqlParameter("@ClassSuffix", classInfo.Suffix),
                            new SqlParameter("@ClassName", classInfo.ClassName)
                        };
                        await _dataService.ExecParamAsync("ODProxl", insertSql, classParams);
                    }
                }
                else
                {
                    string deleteSql = "DELETE FROM sys_model_classes WHERE class_model_id = @ModelId";
                    await _dataService.ExecParamAsync("ODProxl", deleteSql, new SqlParameter("@ModelId", modelId));
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 数据库保存失败: {ex.Message}");
            }
        }

        private string GenerateClassesFileContent(IEnumerable<ClassInfo> classInfos)
        {
            if (classInfos == null || !classInfos.Any())
                return string.Empty;
            var orderedClasses = classInfos.OrderBy(c => c.Suffix);
            return string.Join(Environment.NewLine, orderedClasses.Select(c => c.ClassName));
        }

        private async Task UploadClassesFileAsync(string modelName, string content)
        {
            string classesFileName = Path.ChangeExtension(modelName, ".txt");
            string classesUrl = new Uri(new Uri(_baseUrl), classesFileName).ToString();
            string finalContent = string.IsNullOrWhiteSpace(content) ? "# No classes" : content;

            try
            {
                var stringContent = new StringContent(finalContent, Encoding.UTF8, "application/octet-stream");
                // 上傳 per-model（相容舊模型）
                await _sharedHttpClient.PutAsync(classesUrl, stringContent);
                // 同時覆蓋全域 classes.txt（核心：所有模型共用同一份）
                await _sharedHttpClient.PutAsync(_globalClassesUrl, stringContent);
            }
            catch { }
        }

        private async Task GetUserEnableModelAsync()
        {
            string sql = "SELECT model_name FROM sys_models WHERE model_userAccount = @LoginName";
            var param = new SqlParameter("@LoginName", LoginInfo!.LoginName);
            var result = await _dataService.ScalarParamAsync("ODProxl", sql, param);
            if (!string.IsNullOrWhiteSpace(result))
            {
                var matchedItem = Items.FirstOrDefault(i => i.Name.Equals(result.Trim(), StringComparison.OrdinalIgnoreCase));
                if (matchedItem != null)
                {
                    matchedItem.IsEnabled = true;
                }
            }
        }

        private void FilterItems()
        {
            Items.Clear();
            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allItems
                : _allItems.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
            foreach (var item in filtered)
            {
                Items.Add(item);
            }
        }

        private void ShowDetails(FileSystemItem? item)
        {
            if (item == null) return;
        }
    }
}