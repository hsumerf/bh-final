using Microsoft.Win32;
using System;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace BrailleUrdu
{
    internal static class ActivationManager
    {
        // ── Secret salt embedded in the app ───────────────────────────────────
        private static readonly byte[] _secret =
            Encoding.UTF8.GetBytes("BH-Braille-2026-ActivationSalt-X9K2");

        // ── Registry path ─────────────────────────────────────────────────────
        private const string REG_PATH = @"Software\BHBrailleDesigner";
        private const string REG_KEY  = "ActivationKey";

        // ── Email settings ─────────────────────────────────────────────────────
        private const string SMTP_HOST = "webmail.boltayhuroof.com";
        private const int    SMTP_PORT = 587;
        private const string SMTP_USER = "info@boltayhuroof.com";
        private const string SMTP_PASS = "info@+-12345";
        private const string MAIL_FROM = "info@boltayhuroof.com";
        private const string MAIL_TO   = "H.s.umer.farooq@gmail.com";  // ← replace with your email address

        // ── Machine info ──────────────────────────────────────────────────────

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

        private static string GetMac()
        {
            try
            {
                foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
                    if (nic.OperationalStatus == OperationalStatus.Up &&
                        nic.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                        return nic.GetPhysicalAddress().ToString();
            }
            catch { }
            return "unknown";
        }

        // ── Key derivation ────────────────────────────────────────────────────
        // Produces a 19-char key like "A3B2-C4D1-E5F2-G6H3"
        internal static string DeriveKey(string ip)
        {
            using (var hmac = new HMACSHA256(_secret))
            {
                byte[] hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(ip));
                string hex  = BitConverter.ToString(hash, 0, 8).Replace("-", "").ToUpper();
                return string.Format("{0}-{1}-{2}-{3}",
                    hex.Substring(0,  4), hex.Substring(4,  4),
                    hex.Substring(8,  4), hex.Substring(12, 4));
            }
        }

        // ── Registry helpers ──────────────────────────────────────────────────

        private static string ReadStoredKey()
        {
            try
            {
                using (var k = Registry.CurrentUser.OpenSubKey(REG_PATH))
                    return k?.GetValue(REG_KEY) as string;
            }
            catch { return null; }
        }

        private static void StoreKey(string key)
        {
            try
            {
                using (var k = Registry.CurrentUser.CreateSubKey(REG_PATH))
                    k?.SetValue(REG_KEY, key);
            }
            catch { }
        }

        // ── Public API ────────────────────────────────────────────────────────

        // Returns true if this machine has already been activated with the
        // correct key for its current IP; no network call required.
        internal static bool IsActivated()
        {
            string stored = ReadStoredKey();
            if (string.IsNullOrEmpty(stored)) return false;
            return string.Equals(stored, DeriveKey(GetLocalIp()),
                                 StringComparison.OrdinalIgnoreCase);
        }

        // Fetches public IP and MAC, then emails the developer with the
        // activation key for this machine.  Returns true if email was sent.
        internal static bool SendRequest(string localIp, string key)
        {
            string publicIp = GetPublicIp();
            string mac      = GetMac();
            string body     = "A new activation request has been received.\r\n\r\n"
                            + "Public IP : " + (publicIp ?? "unavailable") + "\r\n"
                            + "Local IP  : " + localIp + "\r\n"
                            + "MAC       : " + mac + "\r\n\r\n"
                            + "Activation key to send to the user:\r\n"
                            + key;
            try
            {
                var mail = new MailMessage();
                mail.To.Add(MAIL_TO);
                mail.From = new MailAddress(MAIL_FROM);
                mail.Subject = "BH Braille Designer - Activation Request";
                mail.Body = body;

                using (var smtp = new SmtpClient(SMTP_HOST, SMTP_PORT))
                {
                    smtp.Timeout = 10000;
                    smtp.UseDefaultCredentials = false;
                    smtp.EnableSsl = false;
                    smtp.Credentials = new NetworkCredential(SMTP_USER, SMTP_PASS);
                    smtp.Send(mail);
                }
                return true;
            }
            catch { return false; }
        }

        private static void SmtpSend(string host, int port,
            string user, string pass,
            string from, string to,
            string subject, string body)
        {
            var mail = new MailMessage();
            mail.To.Add(to);
            mail.From    = new MailAddress(from);
            mail.Subject = subject;
            mail.Body    = body;

            using (var smtp = new SmtpClient(host, port))
            {
                smtp.Timeout               = 10000;
                smtp.UseDefaultCredentials = false;
                smtp.EnableSsl             = false;
                smtp.Credentials           = new NetworkCredential(user, pass);
                smtp.Send(mail);
            }
        }

        // Validates the key the user typed.  If correct, persists it to the
        // registry so subsequent launches are silent.
        internal static bool TryActivate(string enteredKey)
        {
            string expected = DeriveKey(GetLocalIp());
            if (!string.Equals(enteredKey.Trim(), expected,
                               StringComparison.OrdinalIgnoreCase))
                return false;
            StoreKey(expected);
            return true;
        }
    }
}
