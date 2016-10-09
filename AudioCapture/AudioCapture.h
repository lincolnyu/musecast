#pragma once

#include <mmreg.h>

#ifdef __cplusplus
extern "C"
{
#endif
	typedef HRESULT(*__stdcall SetFormatCallback)(WAVEFORMATEX *pwfx);
	typedef HRESULT(*__stdcall CopyDataCallback)(BYTE* pData, UINT32 numFramesAvailable, BOOL *done);

	__declspec(dllexport) HRESULT __stdcall RecordAudioStream(SetFormatCallback setFormat,
		CopyDataCallback copyData);

	__declspec(dllexport) void __stdcall Test();
#ifdef __cplusplus
}
#endif


