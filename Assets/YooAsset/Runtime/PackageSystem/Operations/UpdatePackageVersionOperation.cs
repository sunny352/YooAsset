﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace YooAsset
{
	/// <summary>
	/// 请求远端包裹的最新版本
	/// </summary>
	public abstract class UpdatePackageVersionOperation : AsyncOperationBase
	{
		/// <summary>
		/// 当前最新的包裹版本
		/// </summary>
		public string PackageVersion { protected set; get; }
	}

	/// <summary>
	/// 编辑器下模拟模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class EditorPlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		internal override void Start()
		{
			Status = EOperationStatus.Succeed;
		}
		internal override void Update()
		{
		}
	}

	/// <summary>
	/// 离线模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class OfflinePlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		internal override void Start()
		{
			Status = EOperationStatus.Succeed;
		}
		internal override void Update()
		{
		}
	}

	/// <summary>
	/// 联机模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class HostPlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		private enum ESteps
		{
			None,
			QueryRemotePackageVersion,
			Done,
		}

		private readonly HostPlayModeImpl _impl;
		private readonly string _packageName;
		private readonly bool _appendTimeTicks;
		private readonly int _timeout;
		private readonly int _downloadFailedTryAgain;
		
		private QueryRemotePackageVersionOperation _queryRemotePackageVersionOp;
		private ESteps _steps = ESteps.None;

		internal HostPlayModeUpdatePackageVersionOperation(HostPlayModeImpl impl, string packageName, bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			_impl = impl;
			_packageName = packageName;
			_appendTimeTicks = appendTimeTicks;
			_timeout = timeout;
			_downloadFailedTryAgain = downloadFailedTryAgain;
		}
		internal override void Start()
		{
			_steps = ESteps.QueryRemotePackageVersion;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.QueryRemotePackageVersion)
			{
				if (_queryRemotePackageVersionOp == null)
				{
					_queryRemotePackageVersionOp = new QueryRemotePackageVersionOperation(_impl.RemoteServices, _packageName, _appendTimeTicks, _timeout, _downloadFailedTryAgain);
					OperationSystem.StartOperation(_queryRemotePackageVersionOp);
				}

				if (_queryRemotePackageVersionOp.IsDone == false)
					return;

				if (_queryRemotePackageVersionOp.Status == EOperationStatus.Succeed)
				{
					PackageVersion = _queryRemotePackageVersionOp.PackageVersion;
					_steps = ESteps.Done;
					Status = EOperationStatus.Succeed;
				}
				else
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _queryRemotePackageVersionOp.Error;
				}
			}
		}
	}
	
	/// <summary>
	/// WebGL模式的请求远端包裹的最新版本
	/// </summary>
	internal sealed class WebPlayModeUpdatePackageVersionOperation : UpdatePackageVersionOperation
	{
		private enum ESteps
		{
			None,
			QueryRemotePackageVersion,
			Done,
		}

		private readonly WebPlayModeImpl _impl;
		private readonly string _packageName;
		private readonly bool _appendTimeTicks;
		private readonly int _timeout;
		private readonly int _failedTryAgain;
		private QueryRemotePackageVersionOperation _queryRemotePackageVersionOp;
		private ESteps _steps = ESteps.None;
		
		internal WebPlayModeUpdatePackageVersionOperation(WebPlayModeImpl impl, string packageName, bool appendTimeTicks, int timeout, int downloadFailedTryAgain)
		{
			_impl = impl;
			_packageName = packageName;
			_appendTimeTicks = appendTimeTicks;
			_timeout = timeout;
			_failedTryAgain = downloadFailedTryAgain;
		}
		internal override void Start()
		{
			_steps = ESteps.QueryRemotePackageVersion;
		}
		internal override void Update()
		{
			if (_steps == ESteps.None || _steps == ESteps.Done)
				return;

			if (_steps == ESteps.QueryRemotePackageVersion)
			{
				if (_queryRemotePackageVersionOp == null)
				{
					_queryRemotePackageVersionOp = new QueryRemotePackageVersionOperation(_impl.RemoteServices, _packageName, _appendTimeTicks, _timeout, _failedTryAgain);
					OperationSystem.StartOperation(_queryRemotePackageVersionOp);
				}

				if (_queryRemotePackageVersionOp.IsDone == false)
					return;

				if (_queryRemotePackageVersionOp.Status == EOperationStatus.Succeed)
				{
					PackageVersion = _queryRemotePackageVersionOp.PackageVersion;
					_steps = ESteps.Done;
					Status = EOperationStatus.Succeed;
				}
				else
				{
					_steps = ESteps.Done;
					Status = EOperationStatus.Failed;
					Error = _queryRemotePackageVersionOp.Error;
				}
			}
		}
	}
}