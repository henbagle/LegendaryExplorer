using System.IO;
using LegendaryExplorer.Misc;
using LegendaryExplorerCore.Packages;

namespace LegendaryExplorer.Tools
{
    /// <summary>
    /// A tool that can load a file, and optionally select a UIndex within that file
    /// </summary>
    public interface IFileLoaderTool
    {
        public void LoadFile(string filePath);
        public void LoadFile(string filePath, int uIndex);
    }

    /// <summary>
    /// A tool that can load a file and select a UIndex and a further element within that export by an additional id
    /// </summary>
    /// <remarks>For example, Dialogue Editor can load by conversation UIndex and select a node by StrRef</remarks>
    public interface IOtherFileLoaderTool : IFileLoaderTool
    {
        public void LoadFile(string filePath, int uIndex, int additionalId);
    }

    /// <summary>
    /// Options to use when opening a file in a tool
    /// </summary>
    /// <param name="FilePath">Path to a file for a tool to open</param>
    /// <param name="UIndex">UIndex (or other relevant ID) of an export to select when the tool is opened</param>
    /// <param name="AdditionalId">An additional ID (for example, TLK string id) to use when selecting an element, if the tool supports it</param>
    public record ToolOpenOptionsPackage (string FilePath, int UIndex = 0, int AdditionalId = 0)
    {
        /// <summary>
        /// Create a <see cref="ToolOpenOptionsPackage"/> from an entry's FileRef and UIndex
        /// </summary>
        /// <param name="entry">Entry to get FilePath from</param>
        public ToolOpenOptionsPackage(IEntry entry) : this(entry.FileRef.FilePath, entry.UIndex) { }
    }

    /// <summary>
    /// Helper methods to abstract tool opening logic
    /// </summary>
    public static class ToolOpener
    {
        /// <summary>
        /// Opens the given tool
        /// </summary>
        /// <typeparam name="T">Window class of tool to open</typeparam>
        /// <returns>New instance of tool</returns>
        public static T OpenTool<T>() where T : NotifyPropertyChangedWindowBase, new()
        {
            var tool = new T();
            tool.Show();
            tool.Activate();
            return tool;
        }

        /// <summary>
        /// Opens the given file in the tool
        /// </summary>
        /// <param name="filePath">Path to file to open</param>
        /// <param name="uIndex">Optional: UIndex of element to select</param>
        /// <param name="additionalId">Optional: Additional ID to select upon tool opening, if supported by tool</param>
        /// <typeparam name="T">Tool to open file in</typeparam>
        /// <returns>New instance of tool</returns>
        public static T OpenInTool<T>(string filePath, int uIndex = 0, int additionalId = 0) where T : NotifyPropertyChangedWindowBase, IFileLoaderTool, new()
        {
            return OpenInTool<T>(new ToolOpenOptionsPackage(filePath, uIndex, additionalId));
        }

        /// <summary>
        /// Opens a tool with the given opener options
        /// </summary>
        /// <param name="options">Package of what to open in the tool</param>
        /// <typeparam name="T">Tool to open file in</typeparam>
        /// <returns>New instance of tool</returns>
        public static T OpenInTool<T>(ToolOpenOptionsPackage options) where T : NotifyPropertyChangedWindowBase, IFileLoaderTool, new()
        {
            var tool = new T();
            tool.Show();
            if (options is null || string.IsNullOrEmpty(options.FilePath) || !File.Exists(options.FilePath))
                return tool;

            if (options.UIndex == 0)
            {
                tool.LoadFile(options.FilePath);
            }
            else if (tool is IOtherFileLoaderTool extraTool && options.AdditionalId != 0)
            {
                extraTool.LoadFile(options.FilePath, options.UIndex, options.AdditionalId);
            }
            else
            {
                tool.LoadFile(options.FilePath, options.UIndex);
            }

            tool.Activate();
            return tool;
        }
    }
}