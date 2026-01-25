"""Configuration loading utilities for TimeBase providers."""

import yaml
from typing import Dict, Any
from pathlib import Path
from .models import ProviderConfig


def load_config(config_path: str = "config.yaml") -> ProviderConfig:
    """Load provider configuration from YAML file.
    
    Args:
        config_path: Path to the configuration file
        
    Returns:
        ProviderConfig object
        
    Raises:
        FileNotFoundError: If config file doesn't exist
        yaml.YAMLError: If config file is invalid YAML
        KeyError: If required fields are missing
    """
    path = Path(config_path)
    
    if not path.exists():
        raise FileNotFoundError(f"Configuration file not found: {config_path}")
    
    with open(path, 'r') as f:
        data = yaml.safe_load(f)
    
    if not data:
        raise ValueError(f"Configuration file is empty: {config_path}")
    
    # Validate required fields
    required_fields = ['name', 'version', 'slug']
    missing_fields = [field for field in required_fields if field not in data]
    if missing_fields:
        raise KeyError(f"Missing required configuration fields: {', '.join(missing_fields)}")
    
    return ProviderConfig.from_dict(data)
