namespace Ocelot.Renderers.Models
{
    public class ViewModel : Dictionary<string, string>
    {
        public override string ToString() =>
            string.Join(";", this.Select(kv => $"{Escape(kv.Key)}={Escape(kv.Value)}"));

        public static ViewModel FromString(string serializedData)
        {
            var viewModel = new ViewModel();

            if (string.IsNullOrEmpty(serializedData))
                return viewModel;

            foreach (
                var pair in serializedData.Split(separator, StringSplitOptions.RemoveEmptyEntries)
            )
            {
                var keyValue = pair.Split(['='], 2);
                if (keyValue.Length == 2)
                {
                    viewModel[Unescape(keyValue[0])] = Unescape(keyValue[1]);
                }
            }
            return viewModel;
        }

        private static string Escape(string str) => str.Replace(";", "\\;").Replace("=", "\\=");

        private static string Unescape(string str) => str.Replace("\\;", ";").Replace("\\=", "=");

        private static readonly char[] separator = [';'];
    }
}
