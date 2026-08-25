using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace CodexMonitor
{
    public class AuthInfo
    {
        public string AccessToken { get; set; }
        public string AccountId { get; set; }
        public string Email { get; set; }
        public string PlanType { get; set; }
        public string SubscriptionExpiry { get; set; }

        public AuthInfo()
        {
            AccessToken = "";
            AccountId = "";
            Email = "";
            PlanType = "--";
            SubscriptionExpiry = "--";
        }
    }

    public class QuotaResult
    {
        public bool Success { get; set; }
        public double Percentage { get; set; }
        public string ResetCountdown { get; set; }
        public string ResetDetail { get; set; }
        public string Email { get; set; }
        public string AccountId { get; set; }
        public string PlanType { get; set; }
        public string SubscriptionExpiry { get; set; }
        public string ErrorMsg { get; set; }

        public QuotaResult()
        {
            Success = false;
            Percentage = 100.0;
            ResetCountdown = "--";
            ResetDetail = "";
            Email = "";
            AccountId = "";
            PlanType = "--";
            SubscriptionExpiry = "--";
            ErrorMsg = "";
        }
    }

    public static class ApiService
    {
        private static readonly JavaScriptSerializer Serializer = new JavaScriptSerializer();
        private static readonly object CacheLock = new object();
        private static QuotaResult _cachedResult;
        private static DateTime _lastFetchTime = DateTime.MinValue;
        private const double MinIntervalSec = 2.0;

        static ApiService()
        {
            try
            {
                // 安全开启标准 TLS 1.2 支持，杜绝 NotSupportedException
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12 | SecurityProtocolType.Tls11 | SecurityProtocolType.Tls;
                try
                {
                    // 尝试向下兼容启用 TLS 1.3
                    ServicePointManager.SecurityProtocol |= (SecurityProtocolType)12288;
                }
                catch { }

                ServicePointManager.ServerCertificateValidationCallback = (sender, certificate, chain, sslPolicyErrors) => true;
                ServicePointManager.DefaultConnectionLimit = 32;
                ServicePointManager.Expect100Continue = false;
            }
            catch { }
        }

        private static string FindAuthJsonPath()
        {
            var candidates = new List<string>();

            try
            {
                string p1 = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                if (!string.IsNullOrEmpty(p1)) candidates.Add(Path.Combine(p1, ".codex", "auth.json"));
            }
            catch { }

            try
            {
                string p2 = Environment.GetEnvironmentVariable("USERPROFILE");
                if (!string.IsNullOrEmpty(p2)) candidates.Add(Path.Combine(p2, ".codex", "auth.json"));
            }
            catch { }

            try
            {
                string p3 = Environment.GetEnvironmentVariable("HOME");
                if (!string.IsNullOrEmpty(p3)) candidates.Add(Path.Combine(p3, ".codex", "auth.json"));
            }
            catch { }

            try
            {
                string drive = Environment.GetEnvironmentVariable("HOMEDRIVE");
                string path = Environment.GetEnvironmentVariable("HOMEPATH");
                if (!string.IsNullOrEmpty(drive) && !string.IsNullOrEmpty(path))
                {
                    candidates.Add(Path.Combine(drive + path, ".codex", "auth.json"));
                }
            }
            catch { }

            foreach (var cand in candidates)
            {
                if (File.Exists(cand)) return cand;
            }

            return null;
        }

        public static AuthInfo GetLocalAuth()
        {
            var res = new AuthInfo();
            try
            {
                string authPath = FindAuthJsonPath();
                if (string.IsNullOrEmpty(authPath) || !File.Exists(authPath))
                {
                    return res;
                }

                string text = "";
                using (var fs = new FileStream(authPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                using (var sr = new StreamReader(fs, Encoding.UTF8))
                {
                    text = sr.ReadToEnd();
                }

                if (string.IsNullOrEmpty(text)) return res;

                text = text.Trim('\uFEFF', '\u200B', ' ', '\r', '\n', '\t');
                var dict = Serializer.Deserialize<Dictionary<string, object>>(text);
                if (dict == null) return res;

                string accessToken = "";
                string idToken = "";
                string accountId = "";

                if (dict.ContainsKey("tokens"))
                {
                    var tokens = dict["tokens"] as Dictionary<string, object>;
                    if (tokens != null)
                    {
                        if (tokens.ContainsKey("access_token")) accessToken = Convert.ToString(tokens["access_token"]);
                        if (tokens.ContainsKey("id_token")) idToken = Convert.ToString(tokens["id_token"]);
                        if (tokens.ContainsKey("account_id")) accountId = Convert.ToString(tokens["account_id"]);
                    }
                }

                if (string.IsNullOrEmpty(accessToken) && dict.ContainsKey("access_token"))
                {
                    accessToken = Convert.ToString(dict["access_token"]);
                }
                if (string.IsNullOrEmpty(idToken) && dict.ContainsKey("id_token"))
                {
                    idToken = Convert.ToString(dict["id_token"]);
                }
                if (string.IsNullOrEmpty(accountId) && dict.ContainsKey("account_id"))
                {
                    accountId = Convert.ToString(dict["account_id"]);
                }

                string email = "";
                string subUntilStr = "--";
                string planTypeStr = "ChatGPT Plus";

                string tokenToParse = !string.IsNullOrEmpty(idToken) ? idToken : accessToken;
                if (!string.IsNullOrEmpty(tokenToParse))
                {
                    var parts = tokenToParse.Split('.');
                    if (parts.Length >= 2)
                    {
                        string b64 = parts[1].Replace('-', '+').Replace('_', '/');
                        int rem = b64.Length % 4;
                        if (rem > 0) b64 += new string('=', 4 - rem);
                        byte[] bytes = Convert.FromBase64String(b64);
                        string payloadJson = Encoding.UTF8.GetString(bytes);
                        var pMap = Serializer.Deserialize<Dictionary<string, object>>(payloadJson);
                        if (pMap != null)
                        {
                            if (pMap.ContainsKey("email") && pMap["email"] != null)
                            {
                                email = Convert.ToString(pMap["email"]);
                            }
                            else if (pMap.ContainsKey("https://api.openai.com/profile"))
                            {
                                var prof = pMap["https://api.openai.com/profile"] as Dictionary<string, object>;
                                if (prof != null && prof.ContainsKey("email")) email = Convert.ToString(prof["email"]);
                            }

                            if (pMap.ContainsKey("https://api.openai.com/auth"))
                            {
                                var authObj = pMap["https://api.openai.com/auth"] as Dictionary<string, object>;
                                if (authObj != null)
                                {
                                    if (authObj.ContainsKey("chatgpt_plan_type") && authObj["chatgpt_plan_type"] != null)
                                    {
                                        string pt = Convert.ToString(authObj["chatgpt_plan_type"]).ToLower();
                                        if (pt.Contains("pro")) planTypeStr = "ChatGPT Pro";
                                        else if (pt.Contains("plus")) planTypeStr = "ChatGPT Plus";
                                        else if (pt.Contains("team")) planTypeStr = "ChatGPT Team";
                                        else planTypeStr = "ChatGPT " + char.ToUpper(pt[0]) + pt.Substring(1);
                                    }

                                    if (authObj.ContainsKey("chatgpt_subscription_active_until") && authObj["chatgpt_subscription_active_until"] != null)
                                    {
                                        string rawUntil = Convert.ToString(authObj["chatgpt_subscription_active_until"]);
                                        DateTime dt;
                                        if (DateTime.TryParse(rawUntil, out dt))
                                        {
                                            subUntilStr = dt.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                res.AccessToken = accessToken;
                res.AccountId = accountId;
                res.Email = email;
                res.PlanType = planTypeStr;
                res.SubscriptionExpiry = subUntilStr;
                return res;
            }
            catch { }
            return res;
        }

        public static QuotaResult FetchQuota(bool force)
        {
            lock (CacheLock)
            {
                var now = DateTime.Now;
                if (!force && _cachedResult != null && (now - _lastFetchTime).TotalSeconds < MinIntervalSec)
                {
                    return _cachedResult;
                }

                var auth = GetLocalAuth();
                if (string.IsNullOrEmpty(auth.AccessToken))
                {
                    return new QuotaResult
                    {
                        Success = true,
                        Percentage = 0.0,
                        ResetCountdown = "--",
                        ResetDetail = "请先登录 Codex 官方客户端",
                        Email = "未检测到本地登录凭据",
                        PlanType = "未登录",
                        SubscriptionExpiry = "--"
                    };
                }

                try
                {
                    var req = (HttpWebRequest)WebRequest.Create("https://chatgpt.com/backend-api/wham/usage");
                    req.Method = "GET";
                    req.Timeout = 8000;
                    req.Referer = "https://chatgpt.com/";
                    req.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0.0.0 Safari/537.36";
                    req.Accept = "application/json";
                    req.Headers["Authorization"] = "Bearer " + auth.AccessToken;
                    req.Headers["Origin"] = "https://chatgpt.com";
                    if (!string.IsNullOrEmpty(auth.AccountId))
                    {
                        req.Headers["chatgpt-account-id"] = auth.AccountId;
                    }

                    using (var resp = (HttpWebResponse)req.GetResponse())
                    using (var reader = new StreamReader(resp.GetResponseStream(), Encoding.UTF8))
                    {
                        string json = reader.ReadToEnd();
                        var data = Serializer.Deserialize<Dictionary<string, object>>(json);
                        if (data != null)
                        {
                            string finalEmail = !string.IsNullOrEmpty(auth.Email) ? auth.Email : "ChatGPT 用户";
                            if (data.ContainsKey("email") && data["email"] != null)
                            {
                                finalEmail = Convert.ToString(data["email"]);
                            }

                            string planTypeStr = auth.PlanType;
                            if (data.ContainsKey("plan_type") && data["plan_type"] != null)
                            {
                                string rawPlan = Convert.ToString(data["plan_type"]).ToLower();
                                if (rawPlan.Contains("pro")) planTypeStr = "ChatGPT Pro";
                                else if (rawPlan.Contains("plus")) planTypeStr = "ChatGPT Plus";
                                else if (rawPlan.Contains("team")) planTypeStr = "ChatGPT Team";
                                else planTypeStr = "ChatGPT " + char.ToUpper(rawPlan[0]) + rawPlan.Substring(1);
                            }

                            if (data.ContainsKey("rate_limit"))
                            {
                                var rateLimit = data["rate_limit"] as Dictionary<string, object>;
                                if (rateLimit != null && rateLimit.ContainsKey("primary_window"))
                                {
                                    var primaryWin = rateLimit["primary_window"] as Dictionary<string, object>;
                                    if (primaryWin != null)
                                    {
                                        double usedPercent = 0.0;
                                        if (primaryWin.ContainsKey("used_percent") && primaryWin["used_percent"] != null)
                                        {
                                            usedPercent = Convert.ToDouble(primaryWin["used_percent"]);
                                        }
                                        double remainingPercent = Math.Max(0.0, Math.Min(100.0, 100.0 - usedPercent));

                                        string resetStr = "7d";
                                        string resetDetailStr = "暂无";
                                        if (primaryWin.ContainsKey("reset_after_seconds") && primaryWin["reset_after_seconds"] != null)
                                        {
                                            int sec = Convert.ToInt32(primaryWin["reset_after_seconds"]);
                                            int days = sec / 86400;
                                            int hours = (sec % 86400) / 3600;
                                            int mins = (sec % 3600) / 60;
                                            if (days > 0)
                                            {
                                                resetStr = days + "d";
                                                resetDetailStr = days + "天 " + hours + "小时后";
                                            }
                                            else if (hours > 0)
                                            {
                                                resetStr = hours + "h";
                                                resetDetailStr = hours + "小时 " + mins + "分钟后";
                                            }
                                            else
                                            {
                                                resetStr = mins + "m";
                                                resetDetailStr = mins + "分钟后";
                                            }
                                            var targetTime = now.AddSeconds(sec);
                                            resetDetailStr += " (" + targetTime.ToString("MM-dd HH:mm") + ")";
                                        }

                                        var res = new QuotaResult
                                        {
                                            Success = true,
                                            Percentage = remainingPercent,
                                            ResetCountdown = resetStr,
                                            ResetDetail = resetDetailStr,
                                            Email = finalEmail,
                                            AccountId = auth.AccountId,
                                            PlanType = planTypeStr,
                                            SubscriptionExpiry = auth.SubscriptionExpiry
                                        };
                                        _cachedResult = res;
                                        _lastFetchTime = now;
                                        return res;
                                    }
                                }
                            }
                        }
                    }
                }
                catch (WebException ex)
                {
                    return new QuotaResult
                    {
                        Success = true,
                        Percentage = _cachedResult != null ? _cachedResult.Percentage : 100.0,
                        ResetCountdown = _cachedResult != null ? _cachedResult.ResetCountdown : "--",
                        ResetDetail = "网络连接中 (请开启系统代理)",
                        Email = !string.IsNullOrEmpty(auth.Email) ? auth.Email : "已登录",
                        PlanType = auth.PlanType,
                        SubscriptionExpiry = auth.SubscriptionExpiry,
                        ErrorMsg = ex.Message
                    };
                }
                catch (Exception ex)
                {
                    return new QuotaResult
                    {
                        Success = true,
                        Percentage = 100.0,
                        ResetCountdown = "--",
                        ResetDetail = "网络连接中...",
                        Email = !string.IsNullOrEmpty(auth.Email) ? auth.Email : "已登录",
                        PlanType = auth.PlanType,
                        SubscriptionExpiry = auth.SubscriptionExpiry,
                        ErrorMsg = ex.Message
                    };
                }

                return new QuotaResult { Success = false, ResetCountdown = "--", ErrorMsg = "解析失败" };
            }
        }
    }
}
