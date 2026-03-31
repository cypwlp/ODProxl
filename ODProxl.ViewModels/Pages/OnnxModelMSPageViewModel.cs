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
            LoginInfo = navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
            await LoadModelsFromServerAsync();
        }
        #endregion

        #region 字段
        private readonly string _baseUrl = "http://interior.topmix.net/info/system/software/ODProxl/OnnxModels/";
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly IDataService _dataService;
        private readonly IOnnxModelAnalyzer _onnxModelAnalyzer;
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

        #region 核心載入邏輯
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

                // ========== 新增：本地模型快取邏輯 ==========
                // 本地快取目錄：程式執行目錄下的 ModelsCache
                string cacheDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ModelsCache");
                if (!Directory.Exists(cacheDir))
                    Directory.CreateDirectory(cacheDir);

                // 使用原始檔案名稱作為本地檔名（若擔心衝突可改用 Hash，此處保持簡單）
                string localModelPath = Path.Combine(cacheDir, selectedItem.Name);
                bool needDownload = true;

                // 檢查本地檔案是否存在且大小不為 0
                if (File.Exists(localModelPath))
                {
                    var fileInfo = new FileInfo(localModelPath);
                    if (fileInfo.Length > 0)
                    {
                        needDownload = false;
                        Debug.WriteLine($"📁 使用本地快取模型：{localModelPath}");
                    }
                    else
                    {
                        // 檔案損壞，刪除後重新下載
                        File.Delete(localModelPath);
                        Debug.WriteLine($"⚠️ 本地模型檔案大小為 0，已刪除，將重新下載");
                    }
                }

                if (needDownload)
                {
                    Debug.WriteLine($"🌐 從伺服器下載模型：{selectedItem.FullPath}");
                    using (var httpClient = new HttpClient())
                    {
                        var bytes = await httpClient.GetByteArrayAsync(selectedItem.FullPath);
                        await File.WriteAllBytesAsync(localModelPath, bytes);
                    }
                    Debug.WriteLine($"✅ 模型已下載並儲存至：{localModelPath}");
                }

                // 2. 分析模型（使用本地路徑）
                ModelInfo = await _onnxModelAnalyzer.AnalyzeAsync(localModelPath, deepAnalysis: true);

                // 3. 解析模型类别（以下代碼與您原本完全一致，省略重複...）
                ObservableCollection<ClassInfo>? resultList = null;
                string? raw = ModelInfo?.CustomMetadata?.GetValueOrDefault("names");
                if (!string.IsNullOrWhiteSpace(raw))
                {
                    // 嘗試 JSON 解析
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

                    // 如果 JSON 解析失敗，嘗試手動解析
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

                // 4. 更新類別表（刪除舊記錄 + 插入新記錄）... 以下與您原本代碼完全相同
                if (resultList != null && resultList.Any())
                {
                    string deleteSql = "DELETE FROM sys_model_classes WHERE class_model_id = @ModelId";
                    string deleteResult = await _dataService.ExecParamAsync("ODProxl", deleteSql, new SqlParameter("@ModelId", modelId));
                    if (!IsSuccessResult(deleteResult))
                    {
                        Debug.WriteLine($"❌ 删除旧类别失败: {deleteResult}");
                        return;
                    }

                    string insertSql = @"
                INSERT INTO sys_model_classes (class_model_id, class_suffix, class_name)
                VALUES (@ModelId, @ClassSuffix, @ClassName)";

                    bool allInsertSuccess = true;
                    foreach (var classInfo in resultList)
                    {
                        var classParams = new[]
                        {
                    new SqlParameter("@ModelId", modelId),
                    new SqlParameter("@ClassSuffix", classInfo.Suffix),
                    new SqlParameter("@ClassName", classInfo.ClassName)
                };
                        string insertResult = await _dataService.ExecParamAsync("ODProxl", insertSql, classParams);
                        if (!IsSuccessResult(insertResult))
                        {
                            Debug.WriteLine($"❌ 插入类别失败: {classInfo.ClassName}, 错误: {insertResult}");
                            allInsertSuccess = false;
                        }
                    }
                    if (allInsertSuccess)
                        Debug.WriteLine($"✅ 类别表更新成功，共 {resultList.Count} 条记录");
                    else
                        Debug.WriteLine($"⚠️ 类别表部分插入失败");
                }
                else
                {
                    string deleteSql = "DELETE FROM sys_model_classes WHERE class_model_id = @ModelId";
                    string deleteResult = await _dataService.ExecParamAsync("ODProxl", deleteSql, new SqlParameter("@ModelId", modelId));
                    if (!IsSuccessResult(deleteResult))
                        Debug.WriteLine($"⚠️ 清空类别失败: {deleteResult}");
                    else
                        Debug.WriteLine($"✅ 已清空模型类别（模型无类别元数据）");
                }

                Debug.WriteLine($"✅ 数据库保存成功，model_id = {modelId}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 数据库保存失败: {ex.Message}");
            }
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
                    Debug.WriteLine($"✅ 已設定使用者啟用模型：{matchedItem.Name}");
                }
                else
                {
                    Debug.WriteLine($"⚠️ 使用者啟用模型 '{result}' 不在列表中");
                }
            }
        }

        /// <summary>
        /// 判斷服務執行結果是否成功。
        /// 根據實際服務返回格式，成功可能返回：
        ///   - 純數字（如 "1"、"0"）
        ///   - 包含「已經被執行」等成功關鍵字的訊息
        ///   - 其他明確的成功訊息
        /// </summary>
        private bool IsSuccessResult(string result)
        {
            if (string.IsNullOrWhiteSpace(result))
                return false;

            // 情況1：可解析為整數（包括 0）→ 成功
            if (int.TryParse(result, out _))
                return true;

            // 情況2：包含成功關鍵字（根據實際 WCF 返回調整）
            if (result.Contains("已經被執行") ||
                result.Contains("执行成功") ||
                result.Contains("successfully", StringComparison.OrdinalIgnoreCase))
                return true;

            // 情況3：如果服務明確返回「0 錯誤訊息」且不包含成功關鍵字，則視為失敗
            // 此處可根據需要添加更多判斷
            return false;
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