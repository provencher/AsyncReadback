﻿using UnityEngine;
using System.Collections;					// required for Coroutines
using System.Runtime.InteropServices;		// required for DllImport
using System;								// requred for IntPtr
using System.Text;
using System.Collections.Generic;


/// <summary>
///	Low level interface
/// </summary>
public static class PopReadPixels 
{
#if UNITY_EDITOR_OSX || UNITY_STANDALONE_OSX
	private const string PluginName = "PopReadPixels_Osx";
#elif UNITY_IOS
	//private const string PluginName = "PopReadPixels_Ios";
	private const string PluginName = "__Internal";
#elif UNITY_ANDROID
	private const string PluginName = "PopReadPixels";
#elif UNITY_EDITOR_WIN || UNITY_STANDALONE_WIN
	private const string PluginName = "PopReadPixels";
#else
#error Unsupported platform
#endif

	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern System.IntPtr	PopDebugString();

	[DllImport (PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void			ReleaseDebugString(System.IntPtr String);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int			ReadPixelFromRenderTexture(IntPtr Texture,byte[] PixelData,int PixelDataSize,int[] WidthHeightChannels,RenderTextureFormat PixelFormat);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int			ReadPixelFromTexture2D(IntPtr Texture,byte[] PixelData,int PixelDataSize,int[] WidthHeightChannels,TextureFormat PixelFormat);


	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int			AllocCacheRenderTexture(IntPtr TexturePtr,int Width,int Height,RenderTextureFormat PixelFormat);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int			AllocCacheTexture2D(IntPtr TexturePtr,int Width,int Height,TextureFormat PixelFormat);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void			ReleaseCache(int Cache);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern void			ReadPixelsFromCache(int Cache);

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern IntPtr		GetReadPixelsFromCacheFunc();

	[DllImport(PluginName, CallingConvention = CallingConvention.Cdecl)]
	private static extern int			ReadPixelBytesFromCache(int Cache,byte[] ByteData,int ByteDataSize);




	public class JobCache
	{
		int?		CacheIndex = null;
		int?		LastRevision = null;
		int			Channels = 4;
		byte[]		PixelBytes;
		System.Action<byte[],int,string>	Callback;
		IntPtr		TexturePtr;
		IntPtr		PluginFunction;

		public JobCache(RenderTexture texture,System.Action<byte[],int,string> _Callback)
		{
			TexturePtr = texture.GetNativeTexturePtr();
			CacheIndex = AllocCacheRenderTexture( TexturePtr, texture.width, texture.height, texture.format );
			if ( CacheIndex == -1 )
				throw new System.Exception("Failed to allocate cache index");

			PixelBytes = new byte[texture.width * texture.height * Channels];
			Callback = _Callback;
			PluginFunction = GetReadPixelsFromCacheFunc();
		}

		public JobCache(Texture2D texture,System.Action<byte[],int,string> _Callback)
		{
			TexturePtr = texture.GetNativeTexturePtr();
			CacheIndex = AllocCacheTexture2D( TexturePtr, texture.width, texture.height, texture.format );
			if ( CacheIndex == -1 )
				throw new System.Exception("Failed to allocate cache index");

			PixelBytes = new byte[texture.width * texture.height * Channels];
			Callback = _Callback;
			PluginFunction = GetReadPixelsFromCacheFunc();
		}

		public void ReadAsync()
		{
			GL.IssuePluginEvent( PluginFunction, CacheIndex.Value );
		}

		protected virtual void Finalize()
		{
			Release();
		}

		public bool	HasChanged()
		{
			if ( !this.CacheIndex.HasValue )
				return false;

			//	read & copy latest bytes
			try
			{
				var Revision = ReadPixelBytesFromCache(this.CacheIndex.Value, PixelBytes, PixelBytes.Length);
				if (Revision < 0)
					throw new System.Exception("ReadPixelBytesFromCache returned " + Revision);

				var Changed = LastRevision.HasValue ? (LastRevision.Value != Revision) : true;
				if (Changed)
					Callback.Invoke(PixelBytes, Channels, null);
				LastRevision = Revision;
				return Changed;
			}
			catch (System.Exception e)
			{
				Callback.Invoke (null, 0, e.Message);
				return false;
			}
		}

		public void	Release()
		{
			if ( CacheIndex.HasValue )
				ReleaseCache( CacheIndex.Value );
		}
	}

	public static JobCache ReadPixelsAsync(Texture texture,System.Action<byte[],int,string> Callback)
	{
		Debug.Log ("allocating");
		if ( texture is RenderTexture )
		{
			var Job = new JobCache( texture as RenderTexture, Callback );
			Job.ReadAsync();
			return Job;
		}

		if ( texture is Texture2D )
		{
			var Job = new JobCache( texture as Texture2D, Callback );
			Job.ReadAsync();
			return Job;
		}

		throw new System.Exception("Texture type not handled");
	}

	public static void FlushDebug()
	{
		FlushDebug ((str)=>
			{
				Debug.Log(str);
			}
		);
	}
		
	public static void FlushDebug(System.Action<string> Callback)
	{
		//	gr: this func is crashing unity. But I can't figure out why.
		/*
		int MaxFlushPerFrame = 100;
		int i = 0;
		while (i++ < MaxFlushPerFrame)
		{
			System.IntPtr StringPtr = System.IntPtr.Zero;
			try
			{
				StringPtr = PopDebugString();
			}
			catch
			{
			}

			//	no more strings to pop
			if (StringPtr == System.IntPtr.Zero)
				break;

			try
			{
				string Str = Marshal.PtrToStringAnsi(StringPtr);
				if (Callback != null)
					Callback.Invoke(Str);
				ReleaseDebugString(StringPtr);
			}
			catch
			{
				ReleaseDebugString(StringPtr);
				throw;
			}
		}
	*/
	}

}
