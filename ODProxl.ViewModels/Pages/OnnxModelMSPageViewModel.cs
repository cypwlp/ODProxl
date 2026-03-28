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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace ODProxl.ViewModels.Pages
{
    public class OnnxModelMSPageViewModel : BindableBase, INavigationAware
    {
        #region INavigationAware
        public bool IsNavigationTarget(NavigationContext navigationContext) => true;
        public void OnNavigatedFrom(NavigationContext navigationContext) { }
        public void OnNavigatedTo(NavigationContext navigationContext)
        {
            LoginInfo=navigationContext.Parameters.GetValue<LoginInfo>("LoginInfo");
            _ = LoadModelsFromServerAsync();
        }
        #endregion

        #region 字段
        private readonly string _baseUrl = "http://interior.topmix.net/info/system/software/ODProxl/OnnxModels/";
        private readonly HttpClient _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        private readonly IDataService _dataService;

        private bool _isLoading;
        private List<FileSystemItem> _allItems = new();
        private string _searchText = string.Empty;
        private LoginInfo? loginInfo;
        #endregion

        #region 屬性
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
        public OnnxModelMSPageViewModel(IDataService dataService)
        {
            _dataService = dataService;
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
                }

                _allItems = tempItems.OrderBy(i => i.Name).ToList();

                // 關鍵修正：把已訂閱事件的物件加入到 Items
                foreach (var item in _allItems)
                {
                    Items.Add(item);
                }

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
                string checkSql = "SELECT COUNT(*) FROM sys_models WHERE model_userAccount = @LoginName";
                var checkParam = new SqlParameter("@LoginName", LoginInfo!.LoginName);

                string countStr = await _dataService.ScalarParamAsync("ODProxl", checkSql, checkParam);
                long count = 0;
                long.TryParse(countStr?.Trim() ?? "0", out count);

                string sql = count > 0
                    ? @"UPDATE sys_models 
                       SET model_name = @ModelName, 
                           model_path = @ModelPath
                       WHERE model_userAccount = @LoginName"
                    : @"INSERT INTO sys_models (model_userAccount, model_name, model_path)
                       VALUES (@LoginName, @ModelName, @ModelPath)";

                var parameters = new[]
                {
                    new SqlParameter("@LoginName", LoginInfo.LoginName),
                    new SqlParameter("@ModelName", selectedItem.Name ?? ""),
                    new SqlParameter("@ModelPath", selectedItem.FullPath ?? "")
                };

                await _dataService.ExecParamAsync("ODProxl", sql, parameters);

                Debug.WriteLine($"✅ 資料庫儲存成功：{selectedItem.Name}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ 資料庫儲存失敗: {ex.Message}");
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