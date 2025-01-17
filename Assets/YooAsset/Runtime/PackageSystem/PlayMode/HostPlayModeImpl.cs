﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace YooAsset
{
	internal class HostPlayModeImpl : IPlayModeServices, IBundleServices
	{
		private PackageManifest _activeManifest;

		// 参数相关
		private string _packageName;
		private IBuildinQueryServices _buildinQueryServices;
		private IDeliveryQueryServices _deliveryQueryServices;
		private IRemoteServices _remoteServices;

		public IRemoteServices RemoteServices
		{
			get { return _remoteServices; }
		}

		/// <summary>
		/// 异步初始化
		/// </summary>
		public InitializationOperation InitializeAsync(string packageName, IBuildinQueryServices buildinQueryServices, IDeliveryQueryServices deliveryQueryServices, IRemoteServices remoteServices)
		{
			_packageName = packageName;
			_buildinQueryServices = buildinQueryServices;
			_deliveryQueryServices = deliveryQueryServices;
			_remoteServices = remoteServices;

			var operation = new HostPlayModeInitializationOperation(this, packageName);
			OperationSystem.StartOperation(operation);
			return operation;
		}

		// 下载相关
		private List<BundleInfo> ConvertToDownloadList(List<PackageBundle> downloadList)
		{
			List<BundleInfo> result = new List<BundleInfo>(downloadList.Count);
			foreach (var packageBundle in downloadList)
			{
				var bundleInfo = ConvertToDownloadInfo(packageBundle);
				result.Add(bundleInfo);
			}
			return result;
		}
		private BundleInfo ConvertToDownloadInfo(PackageBundle packageBundle)
		{
			string remoteMainURL = _remoteServices.GetRemoteMainURL(packageBundle.FileName);
			string remoteFallbackURL = _remoteServices.GetRemoteFallbackURL(packageBundle.FileName);
			BundleInfo bundleInfo = new BundleInfo(packageBundle, BundleInfo.ELoadMode.LoadFromRemote, remoteMainURL, remoteFallbackURL);
			return bundleInfo;
		}

		// 查询相关
		private bool IsBuildinPackageBundle(PackageBundle packageBundle)
		{
			return _buildinQueryServices.QueryStreamingAssets(_packageName, packageBundle.FileName);
		}
		private bool IsCachedPackageBundle(PackageBundle packageBundle)
		{
			return CacheSystem.IsCached(packageBundle.PackageName, packageBundle.CacheGUID);
		}
		private bool IsDeliveryPackageBundle(PackageBundle packageBundle)
		{
			return _deliveryQueryServices.QueryDeliveryFiles(_packageName, packageBundle.FileName);
		}
		private DeliveryFileInfo GetDeliveryFileInfo(PackageBundle packageBundle)
		{
			return _deliveryQueryServices.GetDeliveryFileInfo(_packageName, packageBundle.FileName);
		}

		#region IPlayModeServices接口
		public PackageManifest ActiveManifest
		{
			set
			{
				_activeManifest = value;
			}
			get
			{
				return _activeManifest;
			}
		}
		public void FlushManifestVersionFile()
		{
			if (_activeManifest != null)
			{
				PersistentTools.GetPersistent(_packageName).SaveSandboxPackageVersionFile(_activeManifest.PackageVersion);
			}
		}

		UpdatePackageVersionOperation IPlayModeServices.UpdatePackageVersionAsync(bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			var operation = new HostPlayModeUpdatePackageVersionOperation(this, _packageName, appendTimeTicks, timeout, downloadFailedTryAgain);
			OperationSystem.StartOperation(operation);
			return operation;
		}
		UpdatePackageManifestOperation IPlayModeServices.UpdatePackageManifestAsync(string packageVersion, bool autoSaveVersion, bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			var operation = new HostPlayModeUpdatePackageManifestOperation(this, _packageName, packageVersion, autoSaveVersion, appendTimeTicks, timeout, downloadFailedTryAgain);
			OperationSystem.StartOperation(operation);
			return operation;
		}
		PreDownloadContentOperation IPlayModeServices.PreDownloadContentAsync(string packageVersion, bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			var operation = new HostPlayModePreDownloadContentOperation(this, _packageName, packageVersion, appendTimeTicks, timeout, downloadFailedTryAgain);
			OperationSystem.StartOperation(operation);
			return operation;
		}

		ResourceDownloaderOperation IPlayModeServices.CreateResourceDownloaderByAll(int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> downloadList = GetDownloadListByAll(_activeManifest);
			var operation = new ResourceDownloaderOperation(downloadList, downloadingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		public List<BundleInfo> GetDownloadListByAll(PackageManifest manifest)
		{
			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in manifest.BundleList)
			{
				// 忽略分发文件
				if (IsDeliveryPackageBundle(packageBundle))
					continue;

				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				// 忽略APP资源
				if (IsBuildinPackageBundle(packageBundle))
					continue;

				downloadList.Add(packageBundle);
			}

			return ConvertToDownloadList(downloadList);
		}

		ResourceDownloaderOperation IPlayModeServices.CreateResourceDownloaderByTags(string[] tags, int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> downloadList = GetDownloadListByTags(_activeManifest, tags);
			var operation = new ResourceDownloaderOperation(downloadList, downloadingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		public List<BundleInfo> GetDownloadListByTags(PackageManifest manifest, string[] tags)
		{
			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in manifest.BundleList)
			{
				// 忽略分发文件
				if (IsDeliveryPackageBundle(packageBundle))
					continue;

				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				// 忽略APP资源
				if (IsBuildinPackageBundle(packageBundle))
					continue;

				// 如果未带任何标记，则统一下载
				if (packageBundle.HasAnyTags() == false)
				{
					downloadList.Add(packageBundle);
				}
				else
				{
					// 查询DLC资源
					if (packageBundle.HasTag(tags))
					{
						downloadList.Add(packageBundle);
					}
				}
			}

			return ConvertToDownloadList(downloadList);
		}

		ResourceDownloaderOperation IPlayModeServices.CreateResourceDownloaderByPaths(AssetInfo[] assetInfos, int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> downloadList = GetDownloadListByPaths(_activeManifest, assetInfos);
			var operation = new ResourceDownloaderOperation(downloadList, downloadingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		public List<BundleInfo> GetDownloadListByPaths(PackageManifest manifest, AssetInfo[] assetInfos)
		{
			// 获取资源对象的资源包和所有依赖资源包
			List<PackageBundle> checkList = new List<PackageBundle>();
			foreach (var assetInfo in assetInfos)
			{
				if (assetInfo.IsInvalid)
				{
					YooLogger.Warning(assetInfo.Error);
					continue;
				}

				// 注意：如果清单里未找到资源包会抛出异常！
				PackageBundle mainBundle = manifest.GetMainPackageBundle(assetInfo.AssetPath);
				if (checkList.Contains(mainBundle) == false)
					checkList.Add(mainBundle);

				// 注意：如果清单里未找到资源包会抛出异常！
				PackageBundle[] dependBundles = manifest.GetAllDependencies(assetInfo.AssetPath);
				foreach (var dependBundle in dependBundles)
				{
					if (checkList.Contains(dependBundle) == false)
						checkList.Add(dependBundle);
				}
			}

			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in checkList)
			{
				// 忽略分发文件
				if (IsDeliveryPackageBundle(packageBundle))
					continue;

				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				// 忽略APP资源
				if (IsBuildinPackageBundle(packageBundle))
					continue;

				downloadList.Add(packageBundle);
			}

			return ConvertToDownloadList(downloadList);
		}

		ResourceUnpackerOperation IPlayModeServices.CreateResourceUnpackerByAll(int upackingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> unpcakList = GetUnpackListByAll(_activeManifest);
			var operation = new ResourceUnpackerOperation(unpcakList, upackingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		private List<BundleInfo> GetUnpackListByAll(PackageManifest manifest)
		{
			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in manifest.BundleList)
			{
				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				if (IsBuildinPackageBundle(packageBundle))
				{
					downloadList.Add(packageBundle);
				}
			}

			return ManifestTools.ConvertToUnpackInfos(downloadList);
		}

		ResourceUnpackerOperation IPlayModeServices.CreateResourceUnpackerByTags(string[] tags, int upackingMaxNumber, int failedTryAgain, int timeout)
		{
			List<BundleInfo> unpcakList = GetUnpackListByTags(_activeManifest, tags);
			var operation = new ResourceUnpackerOperation(unpcakList, upackingMaxNumber, failedTryAgain, timeout);
			return operation;
		}
		private List<BundleInfo> GetUnpackListByTags(PackageManifest manifest, string[] tags)
		{
			List<PackageBundle> downloadList = new List<PackageBundle>(1000);
			foreach (var packageBundle in manifest.BundleList)
			{
				// 忽略缓存文件
				if (IsCachedPackageBundle(packageBundle))
					continue;

				// 查询DLC资源
				if (IsBuildinPackageBundle(packageBundle))
				{
					if (packageBundle.HasTag(tags))
					{
						downloadList.Add(packageBundle);
					}
				}
			}

			return ManifestTools.ConvertToUnpackInfos(downloadList);
		}
		#endregion

		#region IBundleServices接口
		private BundleInfo CreateBundleInfo(PackageBundle packageBundle)
		{
			if (packageBundle == null)
				throw new Exception("Should never get here !");

			// 查询分发资源
			if (IsDeliveryPackageBundle(packageBundle))
			{
				DeliveryFileInfo deliveryFileInfo = GetDeliveryFileInfo(packageBundle);
				BundleInfo bundleInfo = new BundleInfo(packageBundle, BundleInfo.ELoadMode.LoadFromDelivery, deliveryFileInfo.DeliveryFilePath, deliveryFileInfo.DeliveryFileOffset);
				return bundleInfo;
			}

			// 查询沙盒资源
			if (IsCachedPackageBundle(packageBundle))
			{
				BundleInfo bundleInfo = new BundleInfo(packageBundle, BundleInfo.ELoadMode.LoadFromCache);
				return bundleInfo;
			}

			// 查询APP资源
			if (IsBuildinPackageBundle(packageBundle))
			{
				BundleInfo bundleInfo = new BundleInfo(packageBundle, BundleInfo.ELoadMode.LoadFromStreaming);
				return bundleInfo;
			}

			// 从服务端下载
			return ConvertToDownloadInfo(packageBundle);
		}
		BundleInfo IBundleServices.GetBundleInfo(AssetInfo assetInfo)
		{
			if (assetInfo.IsInvalid)
				throw new Exception("Should never get here !");

			// 注意：如果清单里未找到资源包会抛出异常！
			var packageBundle = _activeManifest.GetMainPackageBundle(assetInfo.AssetPath);
			return CreateBundleInfo(packageBundle);
		}
		BundleInfo[] IBundleServices.GetAllDependBundleInfos(AssetInfo assetInfo)
		{
			if (assetInfo.IsInvalid)
				throw new Exception("Should never get here !");

			// 注意：如果清单里未找到资源包会抛出异常！
			var depends = _activeManifest.GetAllDependencies(assetInfo.AssetPath);
			List<BundleInfo> result = new List<BundleInfo>(depends.Length);
			foreach (var packageBundle in depends)
			{
				BundleInfo bundleInfo = CreateBundleInfo(packageBundle);
				result.Add(bundleInfo);
			}
			return result.ToArray();
		}
		string IBundleServices.GetBundleName(int bundleID)
		{
			return _activeManifest.GetBundleName(bundleID);
		}
		bool IBundleServices.IsServicesValid()
		{
			return _activeManifest != null;
		}
		#endregion
	}
}