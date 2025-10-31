using System.Security.Cryptography.X509Certificates;

namespace SparseMsixTool.Utils
{
    public static class CertificateValidator
    {
        public static bool ValidateCertificate(string certificatePath, string password = "1234567")
        {
            try
            {
                if (!File.Exists(certificatePath))
                {
                    Console.WriteLine($"证书文件不存在: {certificatePath}");
                    return false;
                }

                // 尝试加载证书
                var cert = new X509Certificate2(certificatePath, password);
                
                // 验证证书是否有私钥
                if (!cert.HasPrivateKey)
                {
                    Console.WriteLine($"证书文件无效: 缺少私钥");
                    return false;
                }

                // 检查证书是否过期
                if (cert.NotAfter < DateTime.Now)
                {
                    Console.WriteLine($"警告: 证书已过期 (过期时间: {cert.NotAfter})");
                    return false;
                }

                if (cert.NotBefore > DateTime.Now)
                {
                    Console.WriteLine($"警告: 证书尚未生效 (生效时间: {cert.NotBefore})");
                    return false;
                }

                Console.WriteLine($"证书验证成功:");
                Console.WriteLine($"  主体: {cert.Subject}");
                Console.WriteLine($"  颁发者: {cert.Issuer}");
                Console.WriteLine($"  有效期: {cert.NotBefore:yyyy-MM-dd} 至 {cert.NotAfter:yyyy-MM-dd}");
                
                return true;
            }
            catch (System.Security.Cryptography.CryptographicException ex)
            {
                Console.WriteLine($"证书验证失败: 密码错误或文件损坏 - {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"证书验证失败: {ex.Message}");
                return false;
            }
        }

        public static string? GetCertificateSubject(string certificatePath, string password = "1234567")
        {
            try
            {
                if (!File.Exists(certificatePath))
                {
                    return null;
                }

                var cert = new X509Certificate2(certificatePath, password);
                return cert.Subject;
            }
            catch
            {
                return null;
            }
        }

        public static bool VerifyCertificateMatchesPublisher(string certificatePath, string publisher, string password = "1234567")
        {
            try
            {
                var subject = GetCertificateSubject(certificatePath, password);
                if (subject == null)
                {
                    Console.WriteLine("无法读取证书主体信息");
                    return false;
                }

                if (subject != publisher)
                {
                    Console.WriteLine($"警告: 证书主体与清单发布者不匹配");
                    Console.WriteLine($"  证书主体: {subject}");
                    Console.WriteLine($"  清单发布者: {publisher}");
                    return false;
                }

                Console.WriteLine($"✓ 证书主体与清单发布者匹配: {publisher}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"验证证书匹配失败: {ex.Message}");
                return false;
            }
        }
    }
}