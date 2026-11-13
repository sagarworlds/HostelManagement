using System;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using Microsoft.Web.XmlTransform;
using Xunit;

namespace HostelManagement.Tests
{
    /// <summary>
    /// Publishing a Release build applies Web.Release.config to Web.config. These tests perform that transformation with
    /// the same library Visual Studio and MSBuild use and look at the result, so the settings the deployment guide
    /// promises (HTTPS-only cookies, no debug build, error pages for everyone) are checked on the file that is deployed.
    /// </summary>
    public class ReleaseTransformTests
    {
        private static string ConfigPath(string name)
        {
            return Path.Combine(AppContext.BaseDirectory, "WebConfig", name);
        }

        private static XDocument Transformed()
        {
            using (var document = new XmlTransformableDocument { PreserveWhitespace = true })
            {
                document.Load(ConfigPath("Web.config"));
                using (var transformation = new XmlTransformation(ConfigPath("Web.Release.config")))
                {
                    Assert.True(transformation.Apply(document), "the transformation reported a failure");
                }

                return XDocument.Parse(document.OuterXml);
            }
        }

        private static XElement SystemWeb(XDocument document, string name)
        {
            return document.Root.Element("system.web").Element(name);
        }

        [Fact]
        public void The_source_Web_config_is_the_development_version()
        {
            // Guards the other tests: they only mean something if the file starts out as the thing Release must change.
            XDocument original = XDocument.Load(ConfigPath("Web.config"));

            Assert.Equal("true", (string)SystemWeb(original, "compilation").Attribute("debug"));
            Assert.Equal("false", (string)SystemWeb(original, "httpCookies").Attribute("requireSSL"));
            Assert.Equal("RemoteOnly", (string)SystemWeb(original, "customErrors").Attribute("mode"));
        }

        [Fact]
        public void A_Release_build_is_not_a_debug_build()
        {
            XDocument release = Transformed();

            Assert.Null(SystemWeb(release, "compilation").Attribute("debug"));
            Assert.Equal("4.8", (string)SystemWeb(release, "compilation").Attribute("targetFramework"));
        }

        [Fact]
        public void A_Release_build_only_lets_cookies_travel_over_HTTPS_and_keeps_the_other_cookie_settings()
        {
            XElement cookies = SystemWeb(Transformed(), "httpCookies");

            Assert.Equal("true", (string)cookies.Attribute("requireSSL"));
            Assert.Equal("true", (string)cookies.Attribute("httpOnlyCookies"));
            Assert.Equal("Lax", (string)cookies.Attribute("sameSite"));
        }

        [Fact]
        public void A_Release_build_shows_the_error_page_to_every_visitor_and_keeps_its_redirects()
        {
            XElement errors = SystemWeb(Transformed(), "customErrors");

            Assert.Equal("On", (string)errors.Attribute("mode"));
            Assert.Equal("~/Error.aspx", (string)errors.Attribute("defaultRedirect"));
            Assert.Equal("ResponseRewrite", (string)errors.Attribute("redirectMode"));
            Assert.Equal("~/Error.aspx?code=404", (string)errors.Elements("error").Single(e => (string)e.Attribute("statusCode") == "404").Attribute("redirect"));
        }

        [Fact]
        public void Nothing_else_in_the_file_changes()
        {
            XDocument original = XDocument.Load(ConfigPath("Web.config"));
            XDocument release = Transformed();

            // Put the three intended differences back into the transformed copy; what remains must be identical.
            SystemWeb(release, "compilation").SetAttributeValue("debug", "true");
            SystemWeb(release, "httpCookies").SetAttributeValue("requireSSL", "false");
            SystemWeb(release, "customErrors").SetAttributeValue("mode", "RemoteOnly");

            Assert.True(XNode.DeepEquals(Normalise(original), Normalise(release)),
                "the Release transform changed something other than debug, requireSSL and customErrors mode");
        }

        [Fact]
        public void The_files_that_hold_secrets_and_the_response_headers_survive_the_transform()
        {
            XDocument release = Transformed();

            Assert.Equal("connectionStrings.config", (string)release.Root.Element("connectionStrings").Attribute("configSource"));
            Assert.Equal("appSettings.local.config", (string)release.Root.Element("appSettings").Attribute("file"));
            string[] headers = release.Root.Element("system.webServer").Element("httpProtocol").Element("customHeaders")
                .Elements("add").Select(e => (string)e.Attribute("name")).ToArray();
            Assert.Contains("X-Content-Type-Options", headers);
            Assert.Contains("X-Frame-Options", headers);
            Assert.Contains("Referrer-Policy", headers);
        }

        /// <summary>Attribute order and insignificant whitespace do not matter to ASP.NET, so they must not matter here.</summary>
        private static XDocument Normalise(XDocument document)
        {
            var copy = new XDocument(document);
            foreach (XElement element in copy.Descendants())
            {
                var sorted = element.Attributes().OrderBy(a => a.Name.ToString(), StringComparer.Ordinal).ToList();
                element.RemoveAttributes();
                element.Add(sorted);
            }

            copy.DescendantNodes().OfType<XText>().Where(t => string.IsNullOrWhiteSpace(t.Value)).ToList().ForEach(t => t.Remove());
            copy.DescendantNodes().OfType<XComment>().ToList().ForEach(c => c.Remove());
            return copy;
        }
    }
}
