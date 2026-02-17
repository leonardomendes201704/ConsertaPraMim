import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: 'com.consertapramim.client',
  appName: 'ConsertaPraMim',
  webDir: 'dist',
  server: {
    cleartext: true,
    androidScheme: 'http'
  }
};

export default config;
