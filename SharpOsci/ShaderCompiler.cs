using shaderc;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;


namespace SharpOsci
{
    internal class ShaderCompiler
    {
        public byte[] CompileComputeShader(string path, ShaderKind shaderType)
        {
            Options opt = new Options(false);
            opt.SetTargetEnvironment(TargetEnvironment .Vulkan,EnvironmentVersion.Vulkan_1_2);
            opt.Optimization=OptimizationLevel.Performance;
            var compiler = new Compiler(opt);


            // 执行编译
            var result= compiler.Compile(path, shaderType, "main");


            // 检查编译状态
            if (result.Status != Status.Success)
            {
                throw new Exception($"Shader 编译失败:\n{result.ErrorMessage}");
            }

            // 获取 SPIR-V 二进制数据
            return GetSpirvBytes(result);
        }

        // 从嵌入资源加载 GLSL 源码
        public string LoadEmbeddedShader(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream(resourceName);
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        public byte[] GetSpirvBytes(Result result)
        {
            if (result.Status != Status.Success)
            {
                throw new InvalidOperationException("编译未成功，无法获取 SPIR-V 数据");
            }

            // 获取数据指针和长度
            IntPtr codePtr = result.CodePointer;
            uint codeLength = result.CodeLength;

            if (codePtr == IntPtr.Zero || codeLength == 0)
            {
                throw new InvalidDataException("SPIR-V 数据无效");
            }

            // 将指针数据复制到 byte[]
            byte[] spirvBytes = new byte[codeLength];
            Marshal.Copy(codePtr, spirvBytes, 0, (int)codeLength);
            return spirvBytes;
        }
    }
}
