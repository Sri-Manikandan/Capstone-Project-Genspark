using System.Collections.Concurrent;
using System.Reflection;

namespace EMSBLLLibrary.Emails
{
    public interface IEmailTemplateRenderer
    {
        string Render(string templateKey, IDictionary<string, string> tokens);
    }

    // Substitutes {{Token}} placeholders in an HTML template and wraps the result
    // in the shared layout. Deliberately not a full template engine: templates that
    // need repetition (booking item rows) receive pre-rendered HTML as a token.
    public class EmailTemplateRenderer : IEmailTemplateRenderer
    {
        private static readonly ConcurrentDictionary<string, string> Cache = new();
        private const string ResourcePrefix = "EMSBLLLibrary.Emails.Templates.";

        public string Render(string templateKey, IDictionary<string, string> tokens)
        {
            var layout = Load("_layout");
            var body = Load(templateKey);

            // Body first, so tokens inside the body are substituted by the same pass.
            var html = layout.Replace("{{Body}}", body);

            foreach (var (key, value) in tokens)
                html = html.Replace("{{" + key + "}}", value);

            return html;
        }

        private static string Load(string name) =>
            Cache.GetOrAdd(name, n =>
            {
                var assembly = Assembly.GetExecutingAssembly();
                var resource = ResourcePrefix + n + ".html";

                using var stream = assembly.GetManifestResourceStream(resource)
                    ?? throw new InvalidOperationException($"Email template '{n}' not found as embedded resource '{resource}'.");

                using var reader = new StreamReader(stream);
                return reader.ReadToEnd();
            });
    }
}
