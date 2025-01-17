﻿using System;
using System.Diagnostics;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.SceneManagement;

namespace YooAsset
{
	public class ResourcePackage
	{
		private bool _isInitialize = false;
		private string _initializeError = string.Empty;
		private EOperationStatus _initializeStatus = EOperationStatus.None;
		private EPlayMode _playMode;
		private IBundleServices _bundleServices;
		private IPlayModeServices _playModeServices;
		private AssetSystemImpl _assetSystemImpl;

		/// <summary>
		/// 包裹名
		/// </summary>
		public string PackageName { private set; get; }

		/// <summary>
		/// 初始化状态
		/// </summary>
		public EOperationStatus InitializeStatus
		{
			get { return _initializeStatus; }
		}


		private ResourcePackage()
		{
		}
		internal ResourcePackage(string packageName)
		{
			PackageName = packageName;
		}

		/// <summary>
		/// 更新资源包裹
		/// </summary>
		internal void UpdatePackage()
		{
			if (_assetSystemImpl != null)
				_assetSystemImpl.Update();
		}

		/// <summary>
		/// 销毁资源包裹
		/// </summary>
		internal void DestroyPackage()
		{
			if (_isInitialize)
			{
				_isInitialize = false;
				_initializeError = string.Empty;
				_initializeStatus = EOperationStatus.None;
				_bundleServices = null;
				_playModeServices = null;

				if (_assetSystemImpl != null)
				{
					_assetSystemImpl.ForceUnloadAllAssets();
					_assetSystemImpl = null;
				}
			}
		}

		/// <summary>
		/// 异步初始化
		/// </summary>
		public InitializationOperation InitializeAsync(InitializeParameters parameters)
		{
			// 注意：WebGL平台因为网络原因可能会初始化失败！
			ResetInitializeAfterFailed();

			// 检测初始化参数合法性
			CheckInitializeParameters(parameters);

			// 重写持久化根目录
			var persistent = PersistentTools.GetOrCreatePersistent(PackageName);
			persistent.OverwriteRootDirectory(parameters.BuildinRootDirectory, parameters.SandboxRootDirectory);

			// 初始化资源系统
			InitializationOperation initializeOperation;
			_assetSystemImpl = new AssetSystemImpl();
			if (_playMode == EPlayMode.EditorSimulateMode)
			{
				var editorSimulateModeImpl = new EditorSimulateModeImpl();
				_bundleServices = editorSimulateModeImpl;
				_playModeServices = editorSimulateModeImpl;
				_assetSystemImpl.Initialize(PackageName, true,
					parameters.LoadingMaxTimeSlice, parameters.DownloadFailedTryAgain,
					parameters.DecryptionServices, _bundleServices);

				var initializeParameters = parameters as EditorSimulateModeParameters;
				initializeOperation = editorSimulateModeImpl.InitializeAsync(initializeParameters.SimulateManifestFilePath);
			}
			else if (_playMode == EPlayMode.OfflinePlayMode)
			{
				var offlinePlayModeImpl = new OfflinePlayModeImpl();
				_bundleServices = offlinePlayModeImpl;
				_playModeServices = offlinePlayModeImpl;
				_assetSystemImpl.Initialize(PackageName, false,
					parameters.LoadingMaxTimeSlice, parameters.DownloadFailedTryAgain,
					parameters.DecryptionServices, _bundleServices);

				var initializeParameters = parameters as OfflinePlayModeParameters;
				initializeOperation = offlinePlayModeImpl.InitializeAsync(PackageName);
			}
			else if (_playMode == EPlayMode.HostPlayMode)
			{
				var hostPlayModeImpl = new HostPlayModeImpl();
				_bundleServices = hostPlayModeImpl;
				_playModeServices = hostPlayModeImpl;
				_assetSystemImpl.Initialize(PackageName, false,
					parameters.LoadingMaxTimeSlice, parameters.DownloadFailedTryAgain,
					parameters.DecryptionServices, _bundleServices);

				var initializeParameters = parameters as HostPlayModeParameters;
				initializeOperation = hostPlayModeImpl.InitializeAsync(
					PackageName,
					initializeParameters.BuildinQueryServices,
					initializeParameters.DeliveryQueryServices,
					initializeParameters.RemoteServices
					);
			}
			else if (_playMode == EPlayMode.WebPlayMode)
			{
				var webPlayModeImpl = new WebPlayModeImpl();
				_bundleServices = webPlayModeImpl;
				_playModeServices = webPlayModeImpl;
				_assetSystemImpl.Initialize(PackageName, false,
					parameters.LoadingMaxTimeSlice, parameters.DownloadFailedTryAgain,
					parameters.DecryptionServices, _bundleServices);

				var initializeParameters = parameters as WebPlayModeParameters;
				initializeOperation = webPlayModeImpl.InitializeAsync(
					PackageName,
					initializeParameters.BuildinQueryServices,
					initializeParameters.RemoteServices
					);
			}
			else
			{
				throw new NotImplementedException();
			}

			// 监听初始化结果
			_isInitialize = true;
			initializeOperation.Completed += InitializeOperation_Completed;
			return initializeOperation;
		}
		private void ResetInitializeAfterFailed()
		{
			if (_isInitialize && _initializeStatus == EOperationStatus.Failed)
			{
				_isInitialize = false;
				_initializeStatus = EOperationStatus.None;
				_initializeError = string.Empty;
				_bundleServices = null;
				_playModeServices = null;
				_assetSystemImpl = null;
			}
		}
		private void CheckInitializeParameters(InitializeParameters parameters)
		{
			if (_isInitialize)
				throw new Exception($"{nameof(ResourcePackage)} is initialized yet.");

			if (parameters == null)
				throw new Exception($"{nameof(ResourcePackage)} create parameters is null.");

#if !UNITY_EDITOR
			if (parameters is EditorSimulateModeParameters)
				throw new Exception($"Editor simulate mode only support unity editor.");
#endif

			if (parameters is EditorSimulateModeParameters)
			{
				var editorSimulateModeParameters = parameters as EditorSimulateModeParameters;
				if (string.IsNullOrEmpty(editorSimulateModeParameters.SimulateManifestFilePath))
					throw new Exception($"{nameof(editorSimulateModeParameters.SimulateManifestFilePath)} is null or empty.");
			}

			if (parameters is HostPlayModeParameters)
			{
				var hostPlayModeParameters = parameters as HostPlayModeParameters;
				if (hostPlayModeParameters.BuildinQueryServices == null)
					throw new Exception($"{nameof(IBuildinQueryServices)} is null.");
				if (hostPlayModeParameters.DeliveryQueryServices == null)
					throw new Exception($"{nameof(IDeliveryQueryServices)} is null.");
				if (hostPlayModeParameters.RemoteServices == null)
					throw new Exception($"{nameof(IRemoteServices)} is null.");
			}

			// 鉴定运行模式
			if (parameters is EditorSimulateModeParameters)
				_playMode = EPlayMode.EditorSimulateMode;
			else if (parameters is OfflinePlayModeParameters)
				_playMode = EPlayMode.OfflinePlayMode;
			else if (parameters is HostPlayModeParameters)
				_playMode = EPlayMode.HostPlayMode;
			else if (parameters is WebPlayModeParameters)
				_playMode = EPlayMode.WebPlayMode;
			else
				throw new NotImplementedException();

			// 检测运行时平台
			if (_playMode != EPlayMode.EditorSimulateMode)
			{
#if UNITY_WEBGL
				if (_playMode != EPlayMode.WebPlayMode)
				{
					throw new Exception($"{_playMode} can not support WebGL plateform ! Please use {nameof(EPlayMode.WebPlayMode)}");
				}
#else
				if (_playMode == EPlayMode.WebPlayMode)
				{
					throw new Exception($"{nameof(EPlayMode.WebPlayMode)} only support WebGL plateform !");
				}
#endif
			}

			// 检测参数范围
			if (parameters.LoadingMaxTimeSlice < 10)
			{
				parameters.LoadingMaxTimeSlice = 10;
				YooLogger.Warning($"{nameof(parameters.LoadingMaxTimeSlice)} minimum value is 10 milliseconds.");
			}
			if (parameters.DownloadFailedTryAgain < 1)
			{
				parameters.DownloadFailedTryAgain = 1;
				YooLogger.Warning($"{nameof(parameters.DownloadFailedTryAgain)} minimum value is 1");
			}
		}
		private void InitializeOperation_Completed(AsyncOperationBase op)
		{
			_initializeStatus = op.Status;
			_initializeError = op.Error;
		}

		/// <summary>
		/// 向网络端请求最新的资源版本
		/// </summary>
		/// <param name="appendTimeTicks">在URL末尾添加时间戳</param>
		/// <param name="timeout">超时时间（默认值：60秒）</param>
		/// <param name="downloadFailedTryAgain">下载失败重试次数（默认值：3次）</param>
		public UpdatePackageVersionOperation UpdatePackageVersionAsync(bool appendTimeTicks = true, int timeout = 60, int downloadFailedTryAgain = int.MaxValue)
		{
			DebugCheckInitialize(false);
			return _playModeServices.UpdatePackageVersionAsync(appendTimeTicks, timeout, downloadFailedTryAgain);
		}

		/// <summary>
		/// 向网络端请求并更新清单
		/// </summary>
		/// <param name="packageVersion">更新的包裹版本</param>
		/// <param name="autoSaveVersion">更新成功后自动保存版本号，作为下次初始化的版本。</param>
		/// <param name="appendTimeTicks">在URL末尾添加时间戳</param>
		/// <param name="timeout">超时时间（默认值：60秒）</param>
		/// <param name="downloadFailedTryAgain">下载失败重试次数（默认值：3次）</param>
		public UpdatePackageManifestOperation UpdatePackageManifestAsync(string packageVersion, bool autoSaveVersion = true, bool appendTimeTicks = true, int timeout = 60, int downloadFailedTryAgain = int.MaxValue)
		{
			DebugCheckInitialize(false);
			DebugCheckUpdateManifest();
			return _playModeServices.UpdatePackageManifestAsync(packageVersion, autoSaveVersion, appendTimeTicks, timeout, downloadFailedTryAgain);
		}

		/// <summary>
		/// 预下载指定版本的包裹资源
		/// </summary>
		/// <param name="packageVersion">下载的包裹版本</param>
		/// <param name="appendTimeTicks">在URL末尾添加时间戳</param>
		/// <param name="timeout">超时时间（默认值：60秒）</param>
		/// <param name="downloadFailedTryAgain">下载失败重试次数（默认值：int.MaxValue）</param>
		public PreDownloadContentOperation PreDownloadContentAsync(string packageVersion, bool appendTimeTicks = true, int timeout = 60, int downloadFailedTryAgain = int.MaxValue)
		{
			DebugCheckInitialize(false);
			return _playModeServices.PreDownloadContentAsync(packageVersion, appendTimeTicks, timeout, downloadFailedTryAgain);
		}

		/// <summary>
		/// 清理包裹未使用的缓存文件
		/// </summary>
		public ClearUnusedCacheFilesOperation ClearUnusedCacheFilesAsync()
		{
			DebugCheckInitialize();
			var operation = new ClearUnusedCacheFilesOperation(this);
			OperationSystem.StartOperation(operation);
			return operation;
		}

		/// <summary>
		/// 清理包裹本地所有的缓存文件
		/// </summary>
		public ClearAllCacheFilesOperation ClearAllCacheFilesAsync()
		{
			DebugCheckInitialize();
			var operation = new ClearAllCacheFilesOperation(this);
			OperationSystem.StartOperation(operation);
			return operation;
		}

		/// <summary>
		/// 获取本地包裹的版本信息
		/// </summary>
		public string GetPackageVersion()
		{
			DebugCheckInitialize();
			return _playModeServices.ActiveManifest.PackageVersion;
		}

		/// <summary>
		/// 资源回收（卸载引用计数为零的资源）
		/// </summary>
		public void UnloadUnusedAssets()
		{
			DebugCheckInitialize();
			_assetSystemImpl.Update();
			_assetSystemImpl.UnloadUnusedAssets();
		}

		/// <summary>
		/// 强制回收所有资源
		/// </summary>
		public void ForceUnloadAllAssets()
		{
			DebugCheckInitialize();
			_assetSystemImpl.ForceUnloadAllAssets();
		}

		#region 沙盒相关
		/// <summary>
		/// 获取包裹的内置文件根路径
		/// </summary>
		public string GetPackageBuildinRootDirectory()
		{
			DebugCheckInitialize();
			var persistent = PersistentTools.GetPersistent(PackageName);
			return persistent.BuildinRoot;
		}

		/// <summary>
		/// 获取包裹的沙盒文件根路径
		/// </summary>
		public string GetPackageSandboxRootDirectory()
		{
			DebugCheckInitialize();
			var persistent = PersistentTools.GetPersistent(PackageName);
			return persistent.SandboxRoot;
		}

		/// <summary>
		/// 清空包裹的沙盒目录
		/// </summary>
		public void ClearPackageSandbox()
		{
			DebugCheckInitialize();
			var persistent = PersistentTools.GetPersistent(PackageName);
			persistent.DeleteSandboxPackageFolder();
		}
		#endregion

		#region 资源信息
		/// <summary>
		/// 是否需要从远端更新下载
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public bool IsNeedDownloadFromRemote(string location)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, null);
			if (assetInfo.IsInvalid)
			{
				YooLogger.Warning(assetInfo.Error);
				return false;
			}

			BundleInfo bundleInfo = _bundleServices.GetBundleInfo(assetInfo);
			if (bundleInfo.LoadMode == BundleInfo.ELoadMode.LoadFromRemote)
				return true;
			else
				return false;
		}

		/// <summary>
		/// 是否需要从远端更新下载
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public bool IsNeedDownloadFromRemote(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			if (assetInfo.IsInvalid)
			{
				YooLogger.Warning(assetInfo.Error);
				return false;
			}

			BundleInfo bundleInfo = _bundleServices.GetBundleInfo(assetInfo);
			if (bundleInfo.LoadMode == BundleInfo.ELoadMode.LoadFromRemote)
				return true;
			else
				return false;
		}

		/// <summary>
		/// 获取资源信息列表
		/// </summary>
		/// <param name="tag">资源标签</param>
		public AssetInfo[] GetAssetInfos(string tag)
		{
			DebugCheckInitialize();
			string[] tags = new string[] { tag };
			return _playModeServices.ActiveManifest.GetAssetsInfoByTags(tags);
		}

		/// <summary>
		/// 获取资源信息列表
		/// </summary>
		/// <param name="tags">资源标签列表</param>
		public AssetInfo[] GetAssetInfos(string[] tags)
		{
			DebugCheckInitialize();
			return _playModeServices.ActiveManifest.GetAssetsInfoByTags(tags);
		}

		/// <summary>
		/// 获取资源信息
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public AssetInfo GetAssetInfo(string location)
		{
			DebugCheckInitialize();
			return ConvertLocationToAssetInfo(location, null);
		}

		/// <summary>
		/// 获取资源信息
		/// </summary>
		/// <param name="assetGUID">资源GUID</param>
		public AssetInfo GetAssetInfoByGUID(string assetGUID)
		{
			DebugCheckInitialize();
			return ConvertAssetGUIDToAssetInfo(assetGUID, null);
		}

		/// <summary>
		/// 检查资源定位地址是否有效
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public bool CheckLocationValid(string location)
		{
			DebugCheckInitialize();
			string assetPath = _playModeServices.ActiveManifest.TryMappingToAssetPath(location);
			return string.IsNullOrEmpty(assetPath) == false;
		}
		#endregion

		#region 原生文件
		/// <summary>
		/// 同步加载原生文件
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public RawFileOperationHandle LoadRawFileSync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadRawFileInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载原生文件
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public RawFileOperationHandle LoadRawFileSync(string location)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, null);
			return LoadRawFileInternal(assetInfo, true);
		}

		/// <summary>
		/// 异步加载原生文件
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public RawFileOperationHandle LoadRawFileAsync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadRawFileInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载原生文件
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public RawFileOperationHandle LoadRawFileAsync(string location)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, null);
			return LoadRawFileInternal(assetInfo, false);
		}


		private RawFileOperationHandle LoadRawFileInternal(AssetInfo assetInfo, bool waitForAsyncComplete)
		{
#if UNITY_EDITOR
			if (assetInfo.IsInvalid == false)
			{
				BundleInfo bundleInfo = _bundleServices.GetBundleInfo(assetInfo);
				if (bundleInfo.Bundle.IsRawFile == false)
					throw new Exception($"Cannot load asset bundle file using {nameof(LoadRawFileAsync)} method !");
			}
#endif

			var handle = _assetSystemImpl.LoadRawFileAsync(assetInfo);
			if (waitForAsyncComplete)
				handle.WaitForAsyncComplete();
			return handle;
		}
		#endregion

		#region 场景加载
		/// <summary>
		/// 异步加载场景
		/// </summary>
		/// <param name="location">场景的定位地址</param>
		/// <param name="sceneMode">场景加载模式</param>
		/// <param name="suspendLoad">场景加载到90%自动挂起</param>
		/// <param name="priority">优先级</param>
		public SceneOperationHandle LoadSceneAsync(string location, LoadSceneMode sceneMode = LoadSceneMode.Single, bool suspendLoad = false, int priority = 100)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, null);
			var handle = _assetSystemImpl.LoadSceneAsync(assetInfo, sceneMode, suspendLoad, priority);
			return handle;
		}

		/// <summary>
		/// 异步加载场景
		/// </summary>
		/// <param name="assetInfo">场景的资源信息</param>
		/// <param name="sceneMode">场景加载模式</param>
		/// <param name="suspendLoad">场景加载到90%自动挂起</param>
		/// <param name="priority">优先级</param>
		public SceneOperationHandle LoadSceneAsync(AssetInfo assetInfo, LoadSceneMode sceneMode = LoadSceneMode.Single, bool suspendLoad = false, int priority = 100)
		{
			DebugCheckInitialize();
			var handle = _assetSystemImpl.LoadSceneAsync(assetInfo, sceneMode, suspendLoad, priority);
			return handle;
		}
		#endregion

		#region 资源加载
		/// <summary>
		/// 同步加载资源对象
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public AssetOperationHandle LoadAssetSync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadAssetInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载资源对象
		/// </summary>
		/// <typeparam name="TObject">资源类型</typeparam>
		/// <param name="location">资源的定位地址</param>
		public AssetOperationHandle LoadAssetSync<TObject>(string location) where TObject : UnityEngine.Object
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, typeof(TObject));
			return LoadAssetInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="type">资源类型</param>
		public AssetOperationHandle LoadAssetSync(string location, System.Type type)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAssetInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public AssetOperationHandle LoadAssetSync(string location)
		{
			DebugCheckInitialize();
			Type type = typeof(UnityEngine.Object);
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAssetInternal(assetInfo, true);
		}


		/// <summary>
		/// 异步加载资源对象
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public AssetOperationHandle LoadAssetAsync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadAssetInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载资源对象
		/// </summary>
		/// <typeparam name="TObject">资源类型</typeparam>
		/// <param name="location">资源的定位地址</param>
		public AssetOperationHandle LoadAssetAsync<TObject>(string location) where TObject : UnityEngine.Object
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, typeof(TObject));
			return LoadAssetInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="type">资源类型</param>
		public AssetOperationHandle LoadAssetAsync(string location, System.Type type)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAssetInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public AssetOperationHandle LoadAssetAsync(string location)
		{
			DebugCheckInitialize();
			Type type = typeof(UnityEngine.Object);
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAssetInternal(assetInfo, false);
		}


		private AssetOperationHandle LoadAssetInternal(AssetInfo assetInfo, bool waitForAsyncComplete)
		{
#if UNITY_EDITOR
			if (assetInfo.IsInvalid == false)
			{
				BundleInfo bundleInfo = _bundleServices.GetBundleInfo(assetInfo);
				if (bundleInfo.Bundle.IsRawFile)
					throw new Exception($"Cannot load raw file using {nameof(LoadAssetAsync)} method !");
			}
#endif

			var handle = _assetSystemImpl.LoadAssetAsync(assetInfo);
			if (waitForAsyncComplete)
				handle.WaitForAsyncComplete();
			return handle;
		}
		#endregion

		#region 资源加载
		/// <summary>
		/// 同步加载子资源对象
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public SubAssetsOperationHandle LoadSubAssetsSync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadSubAssetsInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载子资源对象
		/// </summary>
		/// <typeparam name="TObject">资源类型</typeparam>
		/// <param name="location">资源的定位地址</param>
		public SubAssetsOperationHandle LoadSubAssetsSync<TObject>(string location) where TObject : UnityEngine.Object
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, typeof(TObject));
			return LoadSubAssetsInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载子资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="type">子对象类型</param>
		public SubAssetsOperationHandle LoadSubAssetsSync(string location, System.Type type)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadSubAssetsInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载子资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public SubAssetsOperationHandle LoadSubAssetsSync(string location)
		{
			DebugCheckInitialize();
			Type type = typeof(UnityEngine.Object);
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadSubAssetsInternal(assetInfo, true);
		}


		/// <summary>
		/// 异步加载子资源对象
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public SubAssetsOperationHandle LoadSubAssetsAsync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadSubAssetsInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载子资源对象
		/// </summary>
		/// <typeparam name="TObject">资源类型</typeparam>
		/// <param name="location">资源的定位地址</param>
		public SubAssetsOperationHandle LoadSubAssetsAsync<TObject>(string location) where TObject : UnityEngine.Object
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, typeof(TObject));
			return LoadSubAssetsInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载子资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="type">子对象类型</param>
		public SubAssetsOperationHandle LoadSubAssetsAsync(string location, System.Type type)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadSubAssetsInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载子资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public SubAssetsOperationHandle LoadSubAssetsAsync(string location)
		{
			DebugCheckInitialize();
			Type type = typeof(UnityEngine.Object);
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadSubAssetsInternal(assetInfo, false);
		}


		private SubAssetsOperationHandle LoadSubAssetsInternal(AssetInfo assetInfo, bool waitForAsyncComplete)
		{
#if UNITY_EDITOR
			if (assetInfo.IsInvalid == false)
			{
				BundleInfo bundleInfo = _bundleServices.GetBundleInfo(assetInfo);
				if (bundleInfo.Bundle.IsRawFile)
					throw new Exception($"Cannot load raw file using {nameof(LoadSubAssetsAsync)} method !");
			}
#endif

			var handle = _assetSystemImpl.LoadSubAssetsAsync(assetInfo);
			if (waitForAsyncComplete)
				handle.WaitForAsyncComplete();
			return handle;
		}
		#endregion

		#region 资源加载
		/// <summary>
		/// 同步加载资源包内所有资源对象
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public AllAssetsOperationHandle LoadAllAssetsSync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadAllAssetsInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载资源包内所有资源对象
		/// </summary>
		/// <typeparam name="TObject">资源类型</typeparam>
		/// <param name="location">资源的定位地址</param>
		public AllAssetsOperationHandle LoadAllAssetsSync<TObject>(string location) where TObject : UnityEngine.Object
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, typeof(TObject));
			return LoadAllAssetsInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载资源包内所有资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="type">子对象类型</param>
		public AllAssetsOperationHandle LoadAllAssetsSync(string location, System.Type type)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAllAssetsInternal(assetInfo, true);
		}

		/// <summary>
		/// 同步加载资源包内所有资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public AllAssetsOperationHandle LoadAllAssetsSync(string location)
		{
			DebugCheckInitialize();
			Type type = typeof(UnityEngine.Object);
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAllAssetsInternal(assetInfo, true);
		}


		/// <summary>
		/// 异步加载资源包内所有资源对象
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		public AllAssetsOperationHandle LoadAllAssetsAsync(AssetInfo assetInfo)
		{
			DebugCheckInitialize();
			return LoadAllAssetsInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载资源包内所有资源对象
		/// </summary>
		/// <typeparam name="TObject">资源类型</typeparam>
		/// <param name="location">资源的定位地址</param>
		public AllAssetsOperationHandle LoadAllAssetsAsync<TObject>(string location) where TObject : UnityEngine.Object
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, typeof(TObject));
			return LoadAllAssetsInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载资源包内所有资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="type">子对象类型</param>
		public AllAssetsOperationHandle LoadAllAssetsAsync(string location, System.Type type)
		{
			DebugCheckInitialize();
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAllAssetsInternal(assetInfo, false);
		}

		/// <summary>
		/// 异步加载资源包内所有资源对象
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		public AllAssetsOperationHandle LoadAllAssetsAsync(string location)
		{
			DebugCheckInitialize();
			Type type = typeof(UnityEngine.Object);
			AssetInfo assetInfo = ConvertLocationToAssetInfo(location, type);
			return LoadAllAssetsInternal(assetInfo, false);
		}


		private AllAssetsOperationHandle LoadAllAssetsInternal(AssetInfo assetInfo, bool waitForAsyncComplete)
		{
#if UNITY_EDITOR
			if (assetInfo.IsInvalid == false)
			{
				BundleInfo bundleInfo = _bundleServices.GetBundleInfo(assetInfo);
				if (bundleInfo.Bundle.IsRawFile)
					throw new Exception($"Cannot load raw file using {nameof(LoadAllAssetsAsync)} method !");
			}
#endif

			var handle = _assetSystemImpl.LoadAllAssetsAsync(assetInfo);
			if (waitForAsyncComplete)
				handle.WaitForAsyncComplete();
			return handle;
		}
		#endregion

		#region 资源下载
		/// <summary>
		/// 创建资源下载器，用于下载当前资源版本所有的资源包文件
		/// </summary>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateResourceDownloader(int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceDownloaderByAll(downloadingMaxNumber, failedTryAgain, timeout);
		}

		/// <summary>
		/// 创建资源下载器，用于下载指定的资源标签关联的资源包文件
		/// </summary>
		/// <param name="tag">资源标签</param>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateResourceDownloader(string tag, int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceDownloaderByTags(new string[] { tag }, downloadingMaxNumber, failedTryAgain, timeout);
		}

		/// <summary>
		/// 创建资源下载器，用于下载指定的资源标签列表关联的资源包文件
		/// </summary>
		/// <param name="tags">资源标签列表</param>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateResourceDownloader(string[] tags, int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceDownloaderByTags(tags, downloadingMaxNumber, failedTryAgain, timeout);
		}

		/// <summary>
		/// 创建资源下载器，用于下载指定的资源依赖的资源包文件
		/// </summary>
		/// <param name="location">资源的定位地址</param>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateBundleDownloader(string location, int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			var assetInfo = ConvertLocationToAssetInfo(location, null);
			AssetInfo[] assetInfos = new AssetInfo[] { assetInfo };
			return _playModeServices.CreateResourceDownloaderByPaths(assetInfos, downloadingMaxNumber, failedTryAgain, timeout);
		}

		/// <summary>
		/// 创建资源下载器，用于下载指定的资源列表依赖的资源包文件
		/// </summary>
		/// <param name="locations">资源的定位地址列表</param>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateBundleDownloader(string[] locations, int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			List<AssetInfo> assetInfos = new List<AssetInfo>(locations.Length);
			foreach (var location in locations)
			{
				var assetInfo = ConvertLocationToAssetInfo(location, null);
				assetInfos.Add(assetInfo);
			}
			return _playModeServices.CreateResourceDownloaderByPaths(assetInfos.ToArray(), downloadingMaxNumber, failedTryAgain, timeout);
		}

		/// <summary>
		/// 创建资源下载器，用于下载指定的资源依赖的资源包文件
		/// </summary>
		/// <param name="assetInfo">资源信息</param>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateBundleDownloader(AssetInfo assetInfo, int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			AssetInfo[] assetInfos = new AssetInfo[] { assetInfo };
			return _playModeServices.CreateResourceDownloaderByPaths(assetInfos, downloadingMaxNumber, failedTryAgain, timeout);
		}

		/// <summary>
		/// 创建资源下载器，用于下载指定的资源列表依赖的资源包文件
		/// </summary>
		/// <param name="assetInfos">资源信息列表</param>
		/// <param name="downloadingMaxNumber">同时下载的最大文件数</param>
		/// <param name="failedTryAgain">下载失败的重试次数</param>
		/// <param name="timeout">超时时间</param>
		public ResourceDownloaderOperation CreateBundleDownloader(AssetInfo[] assetInfos, int downloadingMaxNumber, int failedTryAgain, int timeout = 60)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceDownloaderByPaths(assetInfos, downloadingMaxNumber, failedTryAgain, timeout);
		}
		#endregion

		#region 资源解压
		/// <summary>
		/// 创建内置资源解压器
		/// </summary>
		/// <param name="tag">资源标签</param>
		/// <param name="unpackingMaxNumber">同时解压的最大文件数</param>
		/// <param name="failedTryAgain">解压失败的重试次数</param>
		public ResourceUnpackerOperation CreateResourceUnpacker(string tag, int unpackingMaxNumber, int failedTryAgain)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceUnpackerByTags(new string[] { tag }, unpackingMaxNumber, failedTryAgain, int.MaxValue);
		}

		/// <summary>
		/// 创建内置资源解压器
		/// </summary>
		/// <param name="tags">资源标签列表</param>
		/// <param name="unpackingMaxNumber">同时解压的最大文件数</param>
		/// <param name="failedTryAgain">解压失败的重试次数</param>
		public ResourceUnpackerOperation CreateResourceUnpacker(string[] tags, int unpackingMaxNumber, int failedTryAgain)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceUnpackerByTags(tags, unpackingMaxNumber, failedTryAgain, int.MaxValue);
		}

		/// <summary>
		/// 创建内置资源解压器
		/// </summary>
		/// <param name="unpackingMaxNumber">同时解压的最大文件数</param>
		/// <param name="failedTryAgain">解压失败的重试次数</param>
		public ResourceUnpackerOperation CreateResourceUnpacker(int unpackingMaxNumber, int failedTryAgain)
		{
			DebugCheckInitialize();
			return _playModeServices.CreateResourceUnpackerByAll(unpackingMaxNumber, failedTryAgain, int.MaxValue);
		}
		#endregion

		#region 内部方法
		/// <summary>
		/// 是否包含资源文件
		/// </summary>
		internal bool IsIncludeBundleFile(string cacheGUID)
		{
			// NOTE : 编辑器模拟模式下始终返回TRUE
			if (_playMode == EPlayMode.EditorSimulateMode)
				return true;
			return _playModeServices.ActiveManifest.IsIncludeBundleFile(cacheGUID);
		}

		private AssetInfo ConvertLocationToAssetInfo(string location, System.Type assetType)
		{
			return _playModeServices.ActiveManifest.ConvertLocationToAssetInfo(location, assetType);
		}
		private AssetInfo ConvertAssetGUIDToAssetInfo(string assetGUID, System.Type assetType)
		{
			return _playModeServices.ActiveManifest.ConvertAssetGUIDToAssetInfo(assetGUID, assetType);
		}
		#endregion

		#region 调试方法
		[Conditional("DEBUG")]
		private void DebugCheckInitialize(bool checkActiveManifest = true)
		{
			if (_initializeStatus == EOperationStatus.None)
				throw new Exception("Package initialize not completed !");
			else if (_initializeStatus == EOperationStatus.Failed)
				throw new Exception($"Package initialize failed ! {_initializeError}");

			if (checkActiveManifest)
			{
				if (_playModeServices.ActiveManifest == null)
					throw new Exception("Not found active manifest !");
			}
		}

		[Conditional("DEBUG")]
		private void DebugCheckUpdateManifest()
		{
			var loadedBundleInfos = _assetSystemImpl.GetLoadedBundleInfos();
			if (loadedBundleInfos.Count > 0)
			{
				YooLogger.Warning($"Found loaded bundle before update manifest ! Recommended to call the  {nameof(ForceUnloadAllAssets)} method to release loaded bundle !");
			}
		}
		#endregion

		#region 调试信息
		internal DebugPackageData GetDebugPackageData()
		{
			DebugPackageData data = new DebugPackageData();
			data.PackageName = PackageName;
			data.ProviderInfos = _assetSystemImpl.GetDebugReportInfos();
			return data;
		}
		#endregion
	}
}