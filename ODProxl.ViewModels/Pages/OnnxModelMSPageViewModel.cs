using Microsoft.Data.SqlClient;
using ODProxl.EntityModels;
using ODProxl.Services;
using Prism.Commands;
using Prism.Mvvm;
using RemoteService;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Pages
{
    public class OnnxModelMSPageViewModel : BindableBase, INavigationAware
    {
        #region INavigationAware
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public async void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoginInfo=navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
            await LoadModelsFromServerAsync();
        }
        #endregion

        #region 字段
        private readonly string _baseUrl = "http://interior.topmix.net/info/system/software/ODProxl/OnnxModels/";
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly IDataService _dataService;
        private readonly IOnnxModelAnalyzer  _onnxModelAnalyzer;     
        private bool _isLoading;
        private List<FileSystemItem> _allItems = new();
        private string _searchText = string.Empty;
        private LoginInfo? loginInfo;
        private OnnxAnalysisResult? _modelInfo;
        private ObservableCollection<ClassInfo> _classInfos;
        #endregion

        #region 屬性
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
        #endregion

        #region 建構函式
        public OnnxModelMSPageViewModel(IDataService dataService, IOnnxModelAnalyzer onnxModelAnalyzer)
        {
            _dataService = dataService;
            _onnxModelAnalyzer = onnxModelAnalyzer;
            SearchCommand = new DelegateCommand(FilterItems);
            ShowDetailsCommand = new DelegateCommand<FileSystemItem>(ShowDetails);

        }
        #endregion

        #region 核心載入邏輯 - 修正版
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

                    // 重要：先訂閱事件，再加入集合
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
                    catch {}
                }

                _allItems = tempItems.OrderBy(i => i.Name).ToList();

                // 關鍵修正：把已訂閱事件的物件加入到 Items
                foreach (var item in _allItems)
                {
                    Items.Add(item);
                }
                await GetUserEnbaleModelAsync();
                Debug.WriteLine($"成功載入 {_allItems.Count} 個 ONNX 模型，並完成事件訂閱");
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
        #endregion

        #region 事件處理
        private async void OnItemEnabledChanged(object? sender, EventArgs e)
        {
            if (sender is not FileSystemItem selectedItem) return;

            // 防止重複觸發
            if (IsLoading) return;

            await SetEnabledModelAsync(selectedItem);
        }
        #endregion

        #region 業務邏輯：互斥 + 儲存
        public async Task SetEnabledModelAsync(FileSystemItem selectedItem)
        {
            if (selectedItem == null || LoginInfo?.LoginName == null) return;

            IsLoading = true;

            try
            {
                // 互斥處理：關閉其他所有項目的 IsEnabled
                foreach (var item in Items)
                {
                    if (item != selectedItem && item.IsEnabled)
                    {
                        item.IsEnabled = false;
                    }
                }

                await SaveSelectedModelAsync(selectedItem);

                Debug.WriteLine($"✅ 已啟用並儲存模型：{selectedItem.Name}");
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
            try
            {
                // 1. 使用 MERGE 实现 UPSERT 并返回 model_id
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
                    Debug.WriteLine("❌ 无法获取 model_id");
                    return;
                }

                string tempFilePath = Path.GetTempFileName();
                using (var httpClient = new HttpClient())
                {
                    var bytes = await httpClient.GetByteArrayAsync(selectedItem.FullPath);
                    await File.WriteAllBytesAsync(tempFilePath, bytes);
                }
                ModelInfo = await _onnxModelAnalyzer.AnalyzeAsync(tempFilePath, deepAnalysis: true);
                // 2. 解析模型类别信息（与原有逻辑一致）
                ObservableCollection<ClassInfo>? resultList = null;

                string? raw = ModelInfo?.CustomMetadata?.GetValueOrDefault("names");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // 尝试 JSON 解析
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

                    // 如果 JSON 解析失败，尝试手动解析（与原逻辑一致）
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

                // 3. 插入类别记录到 sys_model_classes
                if (resultList != null && resultList.Any())
                {
                    string insertClassSql = @"
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
                        await _dataService.ExecParamAsync("ODProxl", insertClassSql, classParams);
                    }
                }
                File.Delete(tempFilePath);
                Debug.WriteLine($"✅ 数据库保存成功，model_id = {modelId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 数据库保存失败: {ex.Message}");
            }
        }
        private async Task GetUserEnbaleModelAsync()
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
                    Debug.WriteLine($"✅ 已設定使用者啟用模型：{matchedItem.Name}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ 使用者啟用模型 '{result}' 不在列表中");
                }
            }
        }
        #endregion

        #region 過濾與詳情
        private void FilterItems()
        {
            Items.Clear();

            var filtered = string.IsNullOrWhiteSpace(SearchText)
                ? _allItems
                : _allItems.Where(i => i.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();

            foreach (var item in filtered)
            {
                // 過濾後也要保持事件訂閱（雖然目前搜尋時事件已訂閱）
                Items.Add(item);
            }
        }

        private void ShowDetails(FileSystemItem? item)
        {
            if (item == null) return;
            Debug.WriteLine($"【詳情】 {item.Name} | {item.FullPath}");
        }
        #endregion
    }
}