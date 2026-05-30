using Microsoft.Win32;
using System;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;

namespace BrailleUrdu
{
    internal static class ActivationManager
    {
        // ── Secret salt embedded in the app ───────────────────────────────────
        private static readonly byte[] _secret =
            Encoding.UTF8.GetBytes("BH-Braille-2026-ActivationSalt-X9K2");

        // ── Registry path ─────────────────────────────────────────────────────
        private const string REG_PATH    = @"Software\BHBrailleDesigner";
        private const string REG_KEY     = "ActivationKey";
        private const string REG_DATE    = "ActivationDate";
        private const string REG_TOKEN   = "DateToken";    // anti-tamper hash for the date
        private const string REG_GEN     = "RenewalGen";   // 0 = first activation, 1+ = renewals

        // ── Email settings ─────────────────────────────────────────────────────
        private const string SMTP_HOST = "webmail.boltayhuroof.com";
        private const int    SMTP_PORT = 587;
        private const string SMTP_USER = "info@boltayhuroof.com";
        private const string SMTP_PASS = "info@+-12345";
        private const string MAIL_FROM = "info@boltayhuroof.com";
        private const string MAIL_TO   = "H.s.umer.farooq@gmail.com";

        private const int LICENSE_DAYS = 365;

        // ── Machine identifier ────────────────────────────────────────────────

        // Returns the MAC of the NIC currently used for internet routing.
        internal static string GetMac()
        {
            try
            {
                string localIp = GetLocalIp();
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.OperationalStatus != OperationalStatus.Up) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    foreach (var addr in nic.GetIPProperties().UnicastAddresses)
                    {
                        if (addr.Address.AddressFamily == AddressFamily.InterNetwork &&
                            addr.Address.ToString() == localIp)
                            return nic.GetPhysicalAddress().ToString();
                    }
                }
                // Fallback: first active non-loopback NIC.
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        return nic.GetPhysicalAddress().ToString();
            }
            catch { }
            return "UNKNOWN";
        }

        internal static string GetLocalIp()
        {
            try
            {
                using (var s = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, 0))
                {
                    s.Connect("8.8.8.8", 65530);
                    return ((IPEndPoint)s.LocalEndPoint).Address.ToString();
                }
            }
            catch { return "0.0.0.0"; }
        }

        private static string GetPublicIp()
        {
            try
            {
                var req = (HttpWebRequest)WebRequest.Create("https://api.ipify.org");
                req.Timeout = 5000;
                using (var resp = req.GetResponse())
                using (var sr   = new System.IO.StreamReader(resp.GetResponseStream()))
                    return sr.ReadToEnd().Trim();
            }
            catch { return null; }
        }

        // ── Key derivation ────────────────────────────────────────────────────
        // Key = HMAC(MAC | renewalGen).
        // Generation 0 = first activation. Generation N = Nth renewal.
        // Each renewal produces a unique key, so the user cannot reuse a prior key.
        private static string DeriveKey(string mac, int renewalGen)
        {
            string input = mac + "|" + renewalGen.ToString();
            using (var hmac = new HMACSHA256(_secret))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                string hex  = BitConverter.ToString(hash, 0, 8).Replace("-", "").ToUpper();
                return string.Format("{0}-{1}-{2}-{3}",
                    hex.Substring(0,  4), hex.Substring(4,  4),
                    hex.Substring(8,  4), hex.Substring(12, 4));
            }
        }

        // Cryptographic token binding the activation date to MAC + generation.
        // If the user edits REG_DATE in the registry the token no longer matches.
        private static string DeriveDateToken(string mac, int renewalGen, string isoDate)
        {
            string input = mac + "|" + renewalGen.ToString() + "|" + isoDate;
            using (var hmac = new HMACSHA256(_secret))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(input));
                return BitConverter.ToString(hash, 0, 8).Replace("-", "").ToUpper();
            }
        }

        // ── Registry helpers ──────────────────────────────────────────────────

        private static string ReadReg(string valueName)
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(REG_PATH))
                    return k?.GetValue(valueName) as string;
            }
            catch { return null; }
        }

        // Returns the stored renewal generation, or -1 if never activated.
        private static int ReadRenewalGen()
        {
            string v = ReadReg(REG_GEN);
            return int.TryParse(v, out int n) ? n : -1;
        }

        private static void StoreActivation(string key, int gen)
        {
            try
            {
                string mac   = GetMac();
                string date  = DateTime.Now.Date.ToString("yyyy-MM-dd");
                string token = DeriveDateToken(mac, gen, date);

                using (var k = Registry.CurrentUser.CreateSubKey(REG_PATH))
                {
                    k?.SetValue(REG_KEY,   key);
                    k?.SetValue(REG_DATE,  date);
                    k?.SetValue(REG_TOKEN, token);
                    k?.SetValue(REG_GEN,   gen.ToString());
                }
            }
            catch { }
        }

        // ── Public API ────────────────────────────────────────────────────────

        // Returns true only when all three conditions hold:
        //   1. Stored key matches HMAC(MAC | stored_generation)
        //   2. Activation date has not been tampered with (DateToken check)
        //   3. Fewer than 365 days have elapsed since activation
        internal static bool IsActivated()
        {
            string mac = GetMac();
            int    gen = ReadRenewalGen();

            if (gen < 0) return false; // never activated

            string storedKey   = ReadReg(REG_KEY);
            string storedDate  = ReadReg(REG_DATE);
            string storedToken = ReadReg(REG_TOKEN);

            if (string.IsNullOrEmpty(storedKey)   ||
                string.IsNullOrEmpty(storedDate)   ||
                string.IsNullOrEmpty(storedToken))
                return false;

            // Key must match current MAC + generation.
            if (!string.Equals(storedKey, DeriveKey(mac, gen),
                                StringComparison.OrdinalIgnoreCase))
                return false;

            // Date token must match — detects registry date tampering.
            if (!string.Equals(storedToken, DeriveDateToken(mac, gen, storedDate),
                                StringComparison.OrdinalIgnoreCase))
                return false;

            // 365-day expiry.
            if (!DateTime.TryParse(storedDate, out DateTime activationDate))
                return false;

            return (DateTime.Now.Date - activationDate.Date).TotalDays < LICENSE_DAYS;
        }

        // True if this device has ever been activated (used to distinguish
        // "never activated" from "license expired" in the dialog message).
        internal static bool HasStoredActivation() => ReadRenewalGen() >= 0;

        // Returns days remaining in current license, or -1 if not activated.
        internal static int DaysRemaining()
        {
            string storedDate = ReadReg(REG_DATE);
            if (!DateTime.TryParse(storedDate, out DateTime d)) return -1;
            int elapsed = (int)(DateTime.Now.Date - d.Date).TotalDays;
            return Math.Max(0, LICENSE_DAYS - elapsed);
        }

        // Computes the next key (gen+1 for renewal, gen 0 for first time),
        // emails the developer, and returns true if email was sent.
        internal static bool SendRequest()
        {
            string mac      = GetMac();
            string publicIp = GetPublicIp();
            int    currentGen = ReadRenewalGen();           // -1 if never activated
            int    nextGen    = Math.Max(0, currentGen + 1); // 0 first time, 1+ renewal
            string key        = DeriveKey(mac, nextGen);

            string label = nextGen == 0 ? "Initial activation" : "Renewal #" + nextGen;
            string body  = "A new activation request has been received.\r\n\r\n"
                         + "Public IP  : " + (publicIp ?? "unavailable") + "\r\n"
                         + "MAC Address: " + mac + "\r\n"
                         + "Request    : " + label + "\r\n\r\n"
                         + "Activation key to send to the user:\r\n"
                         + key + "\r\n\r\n"
                         + "This key grants " + LICENSE_DAYS + " days of use "
                         + "from the date the user enters it.";
            try
            {
                var mail = new MailMessage();
                mail.To.Add(MAIL_TO);
                mail.From    = new MailAddress(MAIL_FROM);
                mail.Subject = "BH Braille Designer – Activation Request (" + label + ")";
                mail.Body    = body;

                using (var smtp = new SmtpClient(SMTP_HOST, SMTP_PORT))
                {
                    smtp.Timeout               = 10000;
                    smtp.UseDefaultCredentials = false;
                    smtp.EnableSsl             = false;
                    smtp.Credentials           = new NetworkCredential(SMTP_USER, SMTP_PASS);
                    smtp.Send(mail);
                }
                return true;
            }
            catch { return false; }
        }

        // Validates the entered key against the expected next generation.
        // On success, stores the new generation + fresh activation date.
        internal static bool TryActivate(string enteredKey)
        {
            string mac      = GetMac();
            int    currentGen = ReadRenewalGen();           // -1 if never activated
            int    nextGen    = Math.Max(0, currentGen + 1);
            string expected   = DeriveKey(mac, nextGen);

            if (!string.Equals(enteredKey.Trim(), expected,
                               StringComparison.OrdinalIgnoreCase))
                return false;

            StoreActivation(expected, nextGen);
            return true;
        }
    }
}
