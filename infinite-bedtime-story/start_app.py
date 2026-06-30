#!/usr/bin/env python3
"""
Startup script for Infinite Bedtime Stories
Runs both backend API and frontend development server
"""

import subprocess
import sys
import os
import time
import signal
from pathlib import Path

def check_dependencies():
    """Check if required dependencies are installed."""
    print("🔍 Checking dependencies...")
    
    # Check Python dependencies
    try:
        import fastapi
        import uvicorn
        import boto3
        print("✅ Python dependencies found")
    except ImportError as e:
        print(f"❌ Missing Python dependency: {e}")
        print("💡 Run: pip install -r requirements_api.txt")
        return False
    
    # Check Node.js and npm
    try:
        subprocess.run("node --version", shell=True, check=True, capture_output=True)
        subprocess.run("npm --version", shell=True, check=True, capture_output=True)
        print("✅ Node.js and npm found")
    except (subprocess.CalledProcessError, FileNotFoundError):
        print("❌ Node.js or npm not found")
        print("💡 Install Node.js from: https://nodejs.org/")
        return False
    
    return True

def setup_environment():
    """Set up environment variables and directories."""
    print("🛠️ Setting up environment...")
    
    # Create static directory for generated files
    os.makedirs("static", exist_ok=True)
    
    # Check for .env file
    if not os.path.exists(".env"):
        print("⚠️ No .env file found. Please configure AWS credentials.")
        print("💡 Copy .env.template to .env and add your credentials")
    
    print("✅ Environment setup complete")

def install_frontend_deps():
    """Install frontend dependencies if needed."""
    frontend_dir = Path("frontend")
    node_modules = frontend_dir / "node_modules"
    
    if not node_modules.exists():
        print("📦 Installing frontend dependencies...")
        try:
            subprocess.run("npm install", cwd=frontend_dir, check=True, shell=True)
            print("✅ Frontend dependencies installed")
        except subprocess.CalledProcessError:
            print("❌ Failed to install frontend dependencies")
            return False
    else:
        print("✅ Frontend dependencies already installed")
    
    return True

def start_backend():
    """Start the FastAPI backend server."""
    print("🚀 Starting backend server...")
    return subprocess.Popen([
        sys.executable, "-m", "uvicorn", 
        "api_server:app", 
        "--host", "0.0.0.0", 
        "--port", "8000", 
        "--reload"
    ])

def start_frontend():
    """Start the React frontend development server."""
    print("🎨 Starting frontend server...")
    return subprocess.Popen("npm run dev", cwd="frontend", shell=True)

def main():
    """Main startup function."""
    print("🌙 Starting Infinite Bedtime Stories...")
    print("=" * 50)
    
    # Check dependencies
    if not check_dependencies():
        sys.exit(1)
    
    # Setup environment
    setup_environment()
    
    # Install frontend dependencies
    if not install_frontend_deps():
        sys.exit(1)
    
    # Start servers
    backend_process = None
    frontend_process = None
    
    try:
        # Start backend
        backend_process = start_backend()
        time.sleep(3)  # Give backend time to start
        
        # Start frontend
        frontend_process = start_frontend()
        time.sleep(2)  # Give frontend time to start
        
        print("\n" + "=" * 50)
        print("🎉 Application started successfully!")
        print("📖 Backend API: http://localhost:8000")
        print("🎨 Frontend App: http://localhost:3000")
        print("📚 API Docs: http://localhost:8000/docs")
        print("=" * 50)
        print("\n💡 Press Ctrl+C to stop both servers")
        
        # Wait for processes
        while True:
            time.sleep(1)
            
            # Check if processes are still running
            if backend_process.poll() is not None:
                print("❌ Backend process stopped unexpectedly")
                break
            
            if frontend_process.poll() is not None:
                print("❌ Frontend process stopped unexpectedly")
                break
    
    except KeyboardInterrupt:
        print("\n🛑 Shutting down servers...")
    
    finally:
        # Clean shutdown
        if backend_process:
            backend_process.terminate()
            try:
                backend_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                backend_process.kill()
        
        if frontend_process:
            frontend_process.terminate()
            try:
                frontend_process.wait(timeout=5)
            except subprocess.TimeoutExpired:
                frontend_process.kill()
        
        print("👋 Goodbye! Sweet dreams!")

if __name__ == "__main__":
    main()