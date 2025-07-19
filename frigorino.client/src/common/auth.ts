// Import the functions you need from the SDKs you need
import { getAnalytics } from "firebase/analytics";
import { initializeApp } from "firebase/app";

// Your web app's Firebase configuration
// For Firebase JS SDK v7.20.0 and later, measurementId is optional
const firebaseConfig = {
    apiKey: "AIzaSyAXJH3Z66XYUA-_7rB7ZQzCDHENBlmUxjs",
    authDomain: "frigorino-2acd1.firebaseapp.com",
    projectId: "frigorino-2acd1",
    storageBucket: "frigorino-2acd1.firebasestorage.app",
    messagingSenderId: "97032277670",
    appId: "1:97032277670:web:970459c8367113abdc2e67",
    measurementId: "G-09LMYWB1XG",
};

// Initialize Firebase
const app = initializeApp(firebaseConfig);
console.log(app);
const analytics = getAnalytics(app);
console.log(analytics);
