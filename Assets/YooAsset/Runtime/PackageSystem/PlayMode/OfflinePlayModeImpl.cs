﻿using System;
using System.Collections;
using System.Collections.Generic;

namespace YooAsset
{
	internal class OfflinePlayModeImpl : IPlayModeServices, IBundleServices
	{
		private PackageManifest _activeManifest;

		/// <summary>
		/// 异步初始化
		/// </summary>
		public InitializationOperation InitializeAsync(string packageName)
		{
			var operation = new OfflinePlayModeInitializationOperation(this, packageName);
			OperationSystem.StartOperation(operation);
			return operation;
		}

		// 查询相关
		private bool IsCachedPackageBundle(PackageBundle packageBundle)
		{
			return CacheSystem.IsCached(packageBundle.PackageName, packageBundle.CacheGUID);
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
		}

		UpdatePackageVersionOperation IPlayModeServices.UpdatePackageVersionAsync(bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			var operation = new OfflinePlayModeUpdatePackageVersionOperation();
			OperationSystem.StartOperation(operation);
			return operation;
		}
		UpdatePackageManifestOperation IPlayModeServices.UpdatePackageManifestAsync(string packageVersion, bool autoSaveVersion, bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			var operation = new OfflinePlayModeUpdatePackageManifestOperation();
			OperationSystem.StartOperation(operation);
			return operation;
		}
		PreDownloadContentOperation IPlayModeServices.PreDownloadContentAsync(string packageVersion, bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			var operation = new OfflinePlayModePreDownloadContentOperation();
			OperationSystem.StartOperation(operation);
			return operation;
		}

		ResourceDownloaderOperation IPlayModeServices.CreateResourceDownloaderByAll(int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			return ResourceDownloaderOperation.CreateEmptyDownloader(downloadingMaxNumber, failedTryAgain, timeout);
		}
		ResourceDownloaderOperation IPlayModeServices.CreateResourceDownloaderByTags(string[] tags, int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			return ResourceDownloaderOperation.CreateEmptyDownloader(downloadingMaxNumber, failedTryAgain, timeout);
		}
		ResourceDownloaderOperation IPlayModeServices.CreateResourceDownloaderByPaths(AssetInfo[] assetInfos, int downloadingMaxNumber, int failedTryAgain, int timeout)
		{
			return ResourceDownloaderOperation.CreateEmptyDownloader(downloadingMaxNumber, failedTryAgain, timeout);
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

				downloadList.Add(packageBundle);
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
				if (packageBundle.HasTag(tags))
				{
					downloadList.Add(packageBundle);
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

			// 查询沙盒资源
			if (CacheSystem.IsCached(packageBundle.PackageName, packageBundle.CacheGUID))
			{
				BundleInfo bundleInfo = new BundleInfo(packageBundle, BundleInfo.ELoadMode.LoadFromCache);
				return bundleInfo;
			}

			// 查询APP资源
			{
				BundleInfo bundleInfo = new BundleInfo(packageBundle, BundleInfo.ELoadMode.LoadFromStreaming);
				return bundleInfo;
			}
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