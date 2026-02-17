import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.consertapramim.provider',
  appName: 'ConsertaPraMim Prestador',
  webDir: 'dist',
  server: {
    cleartext: true,
    androidScheme: 'http'
  }
};

export default config;
