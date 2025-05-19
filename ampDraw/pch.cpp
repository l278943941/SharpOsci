// pch.cpp: 与预编译标头对应的源文件

#include "pch.h"
#include <immintrin.h>
#include <random>


typedef struct {
	UINT32 ImageWidth;//图像宽度
	UINT32 ImageHeight;//图像高度
	UINT32 _bufferStride;//图像数据步幅
	UINT8 PEN_COLOR_R;//画笔颜色R
	UINT8 PEN_COLOR_G;//画笔颜色G
	UINT8 PEN_COLOR_B;//画笔颜色B
	UINT8 PEN_WIDTH;//画笔宽度
	UINT8 TAU;//余晖衰减系数
	DOUBLE sigma;//高斯衰减系数sigma
	UINT8 miuscBitDepth;//音乐位深度
	UINT32 pointArrayLength;//点数组长度
} Params;

float* CLOLORS;
std::random_device rd;  // 用于获取真随机种子（硬件熵源）
std::mt19937 gen(rd());
std::uniform_int_distribution<int> dis(0,1000);
INT32 currentWidth = 0;
INT32 currentHight = 0;
FLOAT PEN_COLOR_B;
FLOAT PEN_COLOR_G;
FLOAT PEN_COLOR_R;


float* PPDarw_getFloats(BYTE* points, Params* p) {
	float* temppoints = new float[p->pointArrayLength / p->miuscBitDepth];
	int addpointnum = 0;
	for (int i = 0; i < p->pointArrayLength / p->miuscBitDepth; i++)
	{
		int offset = i * p->miuscBitDepth;
		int tempint = 0;
		switch (p->miuscBitDepth)
		{
		case 1:
			tempint = points[offset];
			temppoints[i] = *((float*)&tempint);
			break;
		case 2:
			tempint = points[offset] | points[offset + 1] << 8;
			temppoints[i] = tempint;
			break;
		case 3:
			temppoints[i] = (float)(((*(int*)(points + offset)) << 8) >> 8) / (float)8388608;
			break;
		default:
			break;
		}
		addpointnum++;

	}
	return temppoints;

}

void PPDarw_Decay(BYTE* imgptr, Params* p) {

	float brightness = std::exp(-16.0f / p->TAU);

	for (int i = 0; i < p->ImageHeight; i++)
	{
		INT32 index = i * p->_bufferStride;
		for (int j = 0; j < p->ImageWidth; j++)
		{
			//CLOLORS[index] = 0;
			//CLOLORS[index+1] = 0;
			//CLOLORS[index+2] = 0;

			CLOLORS[index]= CLOLORS[index]<1?0: CLOLORS[index] *= brightness;
			CLOLORS[index + 1] = CLOLORS[index + 1]<1? 0:CLOLORS[index + 1]* brightness;
			CLOLORS[index + 2] = CLOLORS[index + 2]<1? 0:CLOLORS[index + 2]* brightness;
			index += 4;
		}
	}

	//衰减函数
}

void PPDarw_Point(float x, float y, BYTE* imgptr, INT64* EncodedOffsets, Params* p,
	float brightness, float pointLiting) {

	int len = (int)EncodedOffsets[0];
	INT64 tempptr = (INT64)(EncodedOffsets);
	tempptr += 8;
	float atten = brightness  * pointLiting*0.25f;//点亮度系数
	for (int i = 0; i < len; i++)
	{
		float atten1 = *((float*)(tempptr));//高斯衰减系数 模拟像素点漏光
		tempptr += 4;
		int yoffset = *((INT16*)(tempptr));
		tempptr += 2;
		int xoffset =  *((INT16*)(tempptr));
		tempptr += 2;
		float atten2 = atten1 * atten;


		UINT64 offset = (((int) (yoffset+y)) * p->_bufferStride + ((int)(xoffset+x)) * 4) + (UINT64)imgptr;
		if (offset > ((UINT64)imgptr + p->ImageHeight * p->_bufferStride) || offset < (UINT64)imgptr)
			return;

		float xatten = (x - (int)x)/2;
		float XaddAtten = (1 - (xatten*2))/2;
		float yatten = (y - (int)y)/2;
		float Yaddatten = (1 - yatten*2)/2;

		INT32 colorindex = ((int)(yoffset + y)) * p->_bufferStride + ((int)(xoffset + x)) * 4;
		CLOLORS[colorindex] = min(CLOLORS[colorindex] + (PEN_COLOR_B * atten2), 255);
		CLOLORS[colorindex + 1] = min(CLOLORS[colorindex +1]+ (PEN_COLOR_G * atten2 ), 255);
		CLOLORS[colorindex + 2] = min(CLOLORS[colorindex + 2] + (PEN_COLOR_R * atten2 ), 255);

		//colorindex = ((int)(yoffset + (y+0.5))) * p->_bufferStride + ((int)(xoffset + x)) * 4;
		//CLOLORS[colorindex] = min(CLOLORS[colorindex] + (PEN_COLOR_B * atten2*(Yaddatten+xatten)), 255);
		//CLOLORS[colorindex + 1] = min(CLOLORS[colorindex + 1] + (PEN_COLOR_G * atten2*(Yaddatten + xatten)), 255);
		//CLOLORS[colorindex + 2] = min(CLOLORS[colorindex + 2] + (PEN_COLOR_R * atten2*(Yaddatten + xatten)), 255);

		//colorindex = ((int)(yoffset + y)) * p->_bufferStride + ((int)(xoffset + (x+0.5))) * 4;
		//CLOLORS[colorindex] = min(CLOLORS[colorindex] + (PEN_COLOR_B * atten2*(XaddAtten+yatten)), 255);
		//CLOLORS[colorindex + 1] = min(CLOLORS[colorindex + 1] + (PEN_COLOR_G * atten2 * (XaddAtten + yatten)), 255);
		//CLOLORS[colorindex + 2] = min(CLOLORS[colorindex + 2] + (PEN_COLOR_R * atten2 * (XaddAtten + yatten)), 255);

		//colorindex = ((int)(yoffset + (y+0.5))) * p->_bufferStride + ((int)(xoffset + (x + 0.5))) * 4;
		//CLOLORS[colorindex] = min(CLOLORS[colorindex] + (PEN_COLOR_B * atten2*(Yaddatten+XaddAtten)), 255);
		//CLOLORS[colorindex + 1] = min(CLOLORS[colorindex + 1] + (PEN_COLOR_G * atten2 * (Yaddatten + XaddAtten)), 255);
		//CLOLORS[colorindex + 2] = min(CLOLORS[colorindex + 2] + (PEN_COLOR_R * atten2 * (Yaddatten + XaddAtten)), 255);

		//if(CLOLORS[colorindex]>1|| CLOLORS[colorindex+1]>1|| CLOLORS[colorindex+2]>1){
		//	printf("%d %d %f %f %f\n", xoffset, yoffset, CLOLORS[colorindex], CLOLORS[colorindex + 1], CLOLORS[colorindex + 2]);
		//
		//}

		//imgptr[colorindex] = CLOLORS[colorindex];
		//imgptr[colorindex+1] = CLOLORS[colorindex+1] ;
		//imgptr[colorindex+2] = CLOLORS[colorindex+2];
		//imgptr[colorindex+3] = CLOLORS[colorindex+3];



	}
}



void PPDarw_Point_noisei(float x, float y, Params* p,
	float brightness) {

	int width = 3;
	float liting = 1.0f / ((width*2+1) * (width*2+1));
	for (int i = -width; i < width + 1; i++) {
		int noisey = i + y;
		for (int j = -width; j < width + 1; j++) {
			int noisex = x + j;
			
			if (noisey<0 || noisey>=p->ImageHeight || noisex<0 || noisex>=p->ImageWidth)
				return;
			INT32 colorindex = noisey * p->_bufferStride + noisex * 4;
			float temp = CLOLORS[colorindex] + p->PEN_COLOR_B * brightness * liting;
			CLOLORS[colorindex] = temp > 255.0f ? 255 : temp;
			temp = CLOLORS[colorindex + 1] + p->PEN_COLOR_G * brightness * liting;
			CLOLORS[colorindex + 1] = temp > 255.0f ? 255 : temp;
			temp = CLOLORS[colorindex + 2] + p->PEN_COLOR_R * brightness * liting;
			CLOLORS[colorindex + 2] = temp > 255.0f ? 255 : temp;
		}


	}



	
}



void PPDarw_Path(INT32 x, INT32 y, INT32 x1, INT32 y1, BYTE* imgptr, INT64* EncodedOffsets, Params* p,
	float brightness, float pathLiting) {
	float len = sqrt(x1 * x1 + y1 * y1);
	float lenadd = 1.0f / len;
	float culen= lenadd;
	int lastxpoint = x;
	int lastypoint = y;

	PPDarw_Point(x, y, imgptr, EncodedOffsets, p, brightness, 0.08f);

	//画噪点
	float u_base_noise = 0.08f;
	for (int noisei = 0; noisei < 7; noisei++)
	{
		float theta = (float)((dis(gen)/1000.0f) * 3.1415926f * 2.0f);
		float u = (dis(gen) / 1000.0f);
		float v = (dis(gen) / 1000.0f);

		// 生成半径（基于指数分布）
		float radiusx = (float)-(log(1 - u) / u_base_noise);

		// 极坐标转笛卡尔坐标
		int noisex = (int)(x + radiusx * cos(theta));
		int noisey = (int)(y + radiusx *sin(theta));  // 关键修正点

		// 边界检查
		if (noisex < 0 || noisex >= p->ImageWidth || noisey < 0 || noisey >= p->ImageHeight)
			continue;

		// 更新像素值
		INT32 colorindex = noisey * p->_bufferStride + noisex * 4;

		PPDarw_Point_noisei(noisex, noisey, p, brightness/5);
	}


	for (float i = 0; i < len; i++)
	{
		culen += lenadd;
		float x2 = (float) (x + (x1 * culen));
		float y2 = (float) (y + (y1 * culen));
		//|| (x2==x+x1&&y2==y+y1)
		if (  (int)x2 == lastxpoint && (int)y2 == lastypoint) {
			continue;
		}
		INT32 colorindex = y2 * p->_bufferStride + x2 * 4;

		//CLOLORS[colorindex] = min(CLOLORS[colorindex] + (PEN_COLOR_B * atten2), 255);
		//CLOLORS[colorindex + 1] = min(CLOLORS[colorindex + 1] + (PEN_COLOR_G * atten2), 255);
		//CLOLORS[colorindex + 2] = min(CLOLORS[colorindex + 2] + (PEN_COLOR_R * atten2), 255);

		PPDarw_Point(x2, y2, imgptr, EncodedOffsets, p, brightness, pathLiting * (lenadd*2));
		lastxpoint = x2;
		lastypoint = y2;

	}
}

INT64* createEncodedOffsets(Params* p) {
	INT64* EncodedOffsets = new INT64[p->PEN_WIDTH * p->PEN_WIDTH];
	int len = 0;
	int oreage = p->PEN_WIDTH / 2;
	for (int i = -oreage; i < oreage; i++)
	{
		for (int j = -oreage; j < oreage; j++) {
			float normDist = std::sqrt((i * i) + (j * j));
			if (normDist <= oreage) {
				float atten = std::exp(-(normDist * normDist) / (2.0f * p->sigma * p->sigma));
				int a = (*(int*)&atten);
				EncodedOffsets[len] = ((UINT64)i) << 48 | ((UINT64)j) << 32 | a;
				len++;
			}
		}
	}
	INT64* RETURN = new INT64[len + 1];
	RETURN[0] = len;
	memcpy(RETURN + 1, EncodedOffsets, sizeof(INT64) * len);
	delete[] EncodedOffsets;
	return RETURN;
}


void PPDarw_POINTANDLINE(BYTE* imgptr, BYTE* points, Params* p) {

	int miuscLen = p->pointArrayLength / p->miuscBitDepth;
	int originX = p->ImageWidth / 2;
	int originY = p->ImageHeight / 2;
	int _scaleX = p->ImageWidth / 2 * 0.99f;
	int _scaleY = p->ImageHeight / 2 * 0.99f;

	INT64* EncodedOffsets = createEncodedOffsets(p);


	int ftame_us = 16000;
	float pointTilme_us = (float)ftame_us / ((float)miuscLen / 2);
	float decay_time = ftame_us;
	float pointTilme_All_ns = pointTilme_us * 1000.0f;

	float pathTime_ns = 4000.0f;
	float pointTilme_ns = pointTilme_All_ns - pathTime_ns;

	float pointliting = (pointTilme_ns / pointTilme_All_ns);
	float pathliting = 5.0f;
		//pointliting * 2.0f;
		//(pathTime_ns / pointTilme_All_ns);
	int lastx = originX;
	int lasty = originY;

	PEN_COLOR_B = p->PEN_COLOR_B;
	PEN_COLOR_G = p->PEN_COLOR_G ;
	PEN_COLOR_R = p->PEN_COLOR_R;

	//炸弹就绪!!!!
	UINT64 fp = (UINT64)points;
	for (int i = 0; i < p->pointArrayLength;i+= p->miuscBitDepth*2) {
		int offset = i ;
		int tempint = 0;
		float x = 0;
		float y = 0;
		switch (p->miuscBitDepth)
		{	case 3:
			x = (float)(((*(int*)(fp)) << 8) >> 8) / (float)8388608;
			fp += 3;
			y = (float)(((*(int*)(fp)) << 8) >> 8) / (float)8388608;
			fp += 3;
			break;
		}

		float brightness = std::exp((-decay_time / (float)1000) / p->TAU);
		decay_time -= pointTilme_us;

		INT32 x1 = x * _scaleX + originX;
		INT32 y1 = - y * _scaleY + originY;
		//PPDarw_Point(x1, y1, imgptr, EncodedOffsets, p, brightness, pointliting);
		PPDarw_Path(x1, y1, (lastx - x1), (lasty - y1), imgptr, EncodedOffsets, p, brightness, pathliting);
		lastx = x1;
		lasty = y1;

	}

	//for (int i = 0; i < miuscLen; i += 2)
	//{
	//	float brightness = std::exp((-decay_time / (float)1000) / p->TAU);
	//	decay_time -= pointTilme_us;

	//	INT32 x = points[i] * _scaleX + originX;
	//	INT32 y = points[i + 1] * _scaleY + originY;
	//	PPDarw_Point(x, y, imgptr, EncodedOffsets, p, brightness, pointliting);
	//	//PPDarw_Path(x, y, (lastx-x), (lasty-y), imgptr, EncodedOffsets, p, brightness, pathliting);
	//	lastx = x;
	//	lasty = y;
	//}

	delete[] EncodedOffsets;
}



void initScreen(UINT64 params)
{
	if (currentWidth == 0) {
		currentWidth = ((Params*)params)->ImageWidth;
		currentHight = ((Params*)params)->ImageHeight;
		CLOLORS = new float[currentWidth * currentHight * 4];
		for (int i = 0; i < currentWidth * currentHight * 4; i += 4)
		{
			CLOLORS[i] = 0;
			CLOLORS[i + 1] = 0;
			CLOLORS[i + 2] = 0;
			CLOLORS[i + 3] = 255;
		}
	}
	else if (currentWidth != ((Params*)params)->ImageWidth || currentHight != ((Params*)params)->ImageHeight) {
		delete[] CLOLORS;
		currentWidth = ((Params*)params)->ImageWidth;
		currentHight = ((Params*)params)->ImageHeight;
		CLOLORS = new float[currentWidth * currentHight * 4];
		for (int i = 0; i < currentWidth * currentHight * 4; i += 4)
		{
			CLOLORS[i] = 0;
			CLOLORS[i + 1] = 0;
			CLOLORS[i + 2] = 0;
			CLOLORS[i + 3] = 255;
		}
	}
}

void showScreen(BYTE* imgptr,Params* p) {
	//for (int i = 0; i < ((p->ImageHeight*p->_bufferStride)/20 * 20); i++)
	//{
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i++];
	//	imgptr[i] = CLOLORS[i];

	//}
	//for(int i = (((p->ImageHeight * p->_bufferStride)/20)*20);i< p->ImageHeight * p->_bufferStride;i++){
	//	imgptr[i] = CLOLORS[i];
	//}


	const int totalPixels = p->ImageHeight*p->_bufferStride ;
	// 每次处理8个像素（AVX2一次处理32字节）
	const int simdPixels = (totalPixels / 32 ) * 32;

	BYTE* tempb = new BYTE[32];

	for (int i = 0; i < simdPixels; i += 32) {
		// 加载8个像素的BGRA（32个float，128字节）
		__m256 bgra0 = _mm256_loadu_ps(CLOLORS+i);
		__m256 bgra1 = _mm256_loadu_ps(CLOLORS+i+8);
		__m256 bgra2 = _mm256_loadu_ps(CLOLORS+i+16);
		__m256 bgra3 = _mm256_loadu_ps(CLOLORS+i+24);


		// 转换为32位整数（截断）
		__m256i bgra0_int = _mm256_cvttps_epi32(bgra0);
		__m256i bgra1_int = _mm256_cvttps_epi32(bgra1);
		__m256i bgra2_int = _mm256_cvttps_epi32(bgra2);
		__m256i bgra3_int = _mm256_cvttps_epi32(bgra3);

		// 打包为16位整数
		__m256i bgra_packed = _mm256_packs_epi16(
			_mm256_packs_epi32(bgra0_int, bgra1_int),
			_mm256_packs_epi32(bgra2_int, bgra3_int)
		);

		// 存储到目标
		_mm256_storeu_si256((__m256i*) tempb , bgra_packed);

		imgptr[i] = tempb[0];
		imgptr[i+1] = tempb[1];
		imgptr[i+2] = tempb[2];
		imgptr[i+3] = tempb[3];

		imgptr[i+4] = tempb[16];
		imgptr[i+5] = tempb[17];
		imgptr[i+6] = tempb[18];
		imgptr[i+7] = tempb[19];

		imgptr[i+8] = tempb[4];
		imgptr[i+9] = tempb[5];
		imgptr[i+10] = tempb[6];
		imgptr[i+11] = tempb[7];

		imgptr[i+12] = tempb[20];
		imgptr[i+13] = tempb[21];
		imgptr[i+14] = tempb[22];
		imgptr[i+15] = tempb[23];

		imgptr[i+16] = tempb[8];
		imgptr[i+17] = tempb[9];
		imgptr[i+18] = tempb[10];
		imgptr[i+19] = tempb[11];

		imgptr[i+20] = tempb[24];
		imgptr[i+21] = tempb[25];
		imgptr[i+22] = tempb[26];
		imgptr[i+23] = tempb[27];

		imgptr[i+24] = tempb[12];
		imgptr[i+25] = tempb[13];
		imgptr[i+26] = tempb[14];
		imgptr[i+27] = tempb[15];

		imgptr[i+28] = tempb[28];
		imgptr[i+29] = tempb[29];
		imgptr[i+30] = tempb[30];
		imgptr[i+31] = tempb[31];
	}
	delete[] tempb;

	// 处理剩余像素
	for (int i = simdPixels; i < totalPixels; i++) {
		imgptr[i] = static_cast<BYTE>(CLOLORS[i]);
	}


	/*float* fs = new float[32];
	byte* bytes = new BYTE[128];
	for (int i = 0; i < 32; i++) {
		fs[i] = i;
	}


	__m256 bgra0 = _mm256_loadu_ps(fs);
	__m256 bgra1 = _mm256_loadu_ps(fs+8);
	__m256 bgra2 = _mm256_loadu_ps(fs+16);
	__m256 bgra3 = _mm256_loadu_ps(fs+24);

	__m256i bgra0_int = _mm256_cvttps_epi32(bgra0);
	__m256i bgra1_int = _mm256_cvttps_epi32(bgra1);
	__m256i bgra2_int = _mm256_cvttps_epi32(bgra2);
	__m256i bgra3_int = _mm256_cvttps_epi32(bgra3);
	
	__m256i bgra_packed = _mm256_packs_epi16(
				_mm256_packs_epi32(bgra0_int, bgra1_int),
				_mm256_packs_epi32(bgra2_int, bgra3_int)
			);
	_mm256_storeu_si256((__m256i*) bytes, bgra_packed);

	for (int i = 0; i < 128; i++) {
		printf("%d:%d    ", i,bytes[i]);
	}*/


}



// 当使用预编译的头时，需要使用此源文件，编译才能成功。
extern "C" __declspec(dllexport) int __cdecl PPDarw_XYScreen(UINT64 imgptr, UINT64 pointsPtr, UINT64 params) {

	initScreen(params);

	//计算时间衰减
	PPDarw_Decay((BYTE*)imgptr, ((Params*)params));
	//生成点偏移数组
	//float* points = PPDarw_getFloats((BYTE*)pointsPtr, ((Params*)params));
	//绘制点和线
	PPDarw_POINTANDLINE((BYTE*)imgptr,(BYTE*) pointsPtr, ((Params*)params));

	showScreen((BYTE*)imgptr, ((Params*)params));
	//释放点偏移数组
	//delete[] points;
	return 12;
}


extern "C" __declspec(dllexport) int __cdecl AmpDarw_XYline(UINT64 imgptr, UINT64 pointsPtr, UINT64 params) {
	return 21;
}







